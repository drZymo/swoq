using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

using Position = Infra.Position;

internal class Game : IGame
{
    private Map map;
    private readonly TimeSpan maxInactivityTime;
    private readonly int maxLevelTicks;
    private readonly TimeSpan maxLevelDuration;
    private readonly Random random;
    private readonly IStatisticsReporter? reporter;

    private readonly DateTime startTime;

    private int ticks = 0;
    private int lastChangeTick = 0;
    private GameStatus status = GameStatus.Active;
    private ImmutableList<Position> player1Positions = [];
    private ImmutableList<Position> player2Positions = [];

    private readonly HashSet<Position> doorsToClose = [];
    private readonly HashSet<Position> doorsToOpen = [];

    public Game(
        Map map,
        TimeSpan maxInactivityTime,
        int maxLevelTicks,
        TimeSpan maxLevelDuration,
        Random random,
        IStatisticsReporter? reporter = null)
    {
        this.map = map;
        this.maxInactivityTime = maxInactivityTime;
        this.maxLevelTicks = maxLevelTicks;
        this.maxLevelDuration = maxLevelDuration;
        this.random = random;
        this.reporter = reporter;

        startTime = Clock.Now;

        State = CreateState();
    }

    public Guid Id { get; } = Guid.NewGuid();
    public GameState State { get; private set; }
    public DateTime LastActionTime { get; private set; } = Clock.Now;
    public bool IsFinished => status != GameStatus.Active || TimedOut || NoProgress;

    private bool TimedOut => (Clock.Now - LastActionTime) > maxInactivityTime;

    private bool NoProgress => (ticks >= maxLevelTicks) || (Clock.Now - startTime) >= maxLevelDuration;

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        // Store current state, so it can be reverted to when something went wrong
        var prevState = (map, player1Positions, player2Positions);

        try
        {
            // Pre condition checks
            if (status == GameStatus.Active && TimedOut) status = GameStatus.FinishedTimeout;
            if (status == GameStatus.Active && NoProgress) status = GameStatus.FinishedNoProgress;
            if (IsFinished) throw new GameFinishedException();

            // As long as game is not finished one of the two players should be active
            Debug.Assert(map.Player1 != null || map.Player2 != null);

            // Make sure actions are given for players that are present
            if (action1 != null && action1 != DirectedAction.None)
            {
                if (map.Player1 == null || !map.Player1.IsPresent) throw new Player1NotPresentException();
                Debug.Assert(map.Player1.IsAlive);
            }
            if (action2 != null && action2 != DirectedAction.None)
            {
                if (map.Player2 == null || !map.Player2.IsPresent) throw new Player2NotPresentException();
                Debug.Assert(map.Player2.IsAlive);
            }

            // All door actions should have been processed
            Debug.Assert(doorsToClose.Count == 0);
            Debug.Assert(doorsToOpen.Count == 0);

            // Act
            PerformPlayerAction(action1, map.Player1);
            PerformPlayerAction(action2, map.Player2);

            ProcessDoors();

            // Process enemies using previous positions of players
            foreach (var enemy in map.Enemies)
            {
                ProcessEnemy(enemy, prevState.map.Player1, prevState.map.Player2);
            }

            // Cleanup characters that might have been killed
            CleanupDeadCharacters();

            // Store current positions to be used for idle detection
            StorePlayerPosition(map.Player1, ref player1Positions);
            StorePlayerPosition(map.Player2, ref player2Positions);

            // Update game status
            LastActionTime = Clock.Now;
            ticks++;
            UpdateGameStatus();
        }
        catch
        {
            // Revert state on any exception, so the user can try again.
            (map, player1Positions, player2Positions) = prevState;
            doorsToClose.Clear();
            doorsToOpen.Clear();
            throw;
        }
        finally
        {
            // Always update state at the end
            State = CreateState();
        }
    }

    public void Cancel()
    {
        status = GameStatus.FinishedCancelled;
    }

    private void UpdateGameStatus()
    {
        if (status != GameStatus.Active) return;

        // Check if one of the players has died
        if (map.Player1 != null && !map.Player1.IsAlive)
        {
            status = GameStatus.FinishedPlayerDied;
            return;
        }
        if (map.Player2 != null && !map.Player2.IsAlive)
        {
            status = GameStatus.FinishedPlayer2Died;
            return;
        }

        // Check if both players exited the map alive
        if ((map.Player1 == null || (!map.Player1.IsPresent && map.Player1.IsAlive)) &&
            (map.Player2 == null || (!map.Player2.IsPresent && map.Player2.IsAlive)))
        {
            status = GameStatus.FinishedSuccess;
            // Report that game is finished
            reporter?.GameFinishedSuccessfully(Id, map.Level, ticks);
            return;
        }

        // Time since last change was too long ago
        if ((ticks - lastChangeTick) > Parameters.MaxNoProgressTicks)
        {
            status = GameStatus.FinishedNoProgress;
            return;
        }

        // Check if player positions have changed
        if (player1Positions.Count > 0 || player2Positions.Count > 0)
        {
            var player1Active = IsPlayerActive(map.Player1, player1Positions);
            var player2Active = IsPlayerActive(map.Player2, player2Positions);
            if (!player1Active && !player2Active)
            {
                status = GameStatus.FinishedNoProgress;
                return;
            }
        }
    }

    private GameState CreateState()
    {
        Debug.Assert(map.Player1 != null || map.Player2 != null);
        PlayerState? player1State = map.Player1 != null ? GetPlayerState(map.Player1) : null;
        PlayerState? player2State = map.Player2 != null ? GetPlayerState(map.Player2) : null;
        return new GameState(ticks, map.Level, status, player1State, player2State);
    }

    private static void StorePlayerPosition(Player? player, ref ImmutableList<Position> playerPositions)
    {
        // Only store positions of active players
        if (player == null || !player.IsPresent) return;

        // Store current position
        playerPositions = playerPositions.Add(player.Position);
        // Cleanup old entries
        while (playerPositions.Count > Parameters.MaxInactivityTicks)
        {
            playerPositions = playerPositions.RemoveAt(0);
        }
    }

    private void PerformPlayerAction(DirectedAction? action, Player? player)
    {
        // Skip if no action requested
        if (!action.HasValue) return;

        // Skip if player has died before action could be performed.
        if (player == null || !player.IsAlive || !player.IsPresent) return;

        var actionPos = GetDirectedActionPosition(player, action.Value);

        switch (action.Value)
        {
            case DirectedAction.None:
                break;
            case DirectedAction.MoveNorth:
            case DirectedAction.MoveEast:
            case DirectedAction.MoveSouth:
            case DirectedAction.MoveWest:
                Move(ref player, actionPos);
                break;
            case DirectedAction.UseNorth:
            case DirectedAction.UseEast:
            case DirectedAction.UseSouth:
            case DirectedAction.UseWest:
                Use(ref player, actionPos);
                break;
            default:
                throw new UnknownActionException();
        }

        map = map.SetCharacter(player);
    }

    private Position GetDirectedActionPosition(Player player, DirectedAction action) => action switch
    {
        DirectedAction.None => player.Position,

        DirectedAction.MoveNorth => map.Pos(player.Position.y - 1, player.Position.x),
        DirectedAction.UseNorth => map.Pos(player.Position.y - 1, player.Position.x),

        DirectedAction.MoveEast => map.Pos(player.Position.y, player.Position.x + 1),
        DirectedAction.UseEast => map.Pos(player.Position.y, player.Position.x + 1),

        DirectedAction.MoveSouth => map.Pos(player.Position.y + 1, player.Position.x),
        DirectedAction.UseSouth => map.Pos(player.Position.y + 1, player.Position.x),

        DirectedAction.MoveWest => map.Pos(player.Position.y, player.Position.x - 1),
        DirectedAction.UseWest => map.Pos(player.Position.y, player.Position.x - 1),

        _ => throw new UnknownActionException(),
    };

    private void Move(ref Player player, Position movePos)
    {
        // Check if entering is allowed
        if (!CanMoveTo(movePos))
        {
            throw new MoveNotAllowedException();
        }

        // Change pos first
        // so player can be crushed by a door that closes by leaving its current cell
        var prevPos = player.Position;
        player = player with { Position = movePos };

        // First leave the current cell,
        // so any doors are closed before entering the next cell
        LeaveCell(prevPos);
        // and enter if still alive
        if (player.IsPresent)
        {
            EnterCell(ref player, player.Position);
        }
    }

    private void LeaveCell(Position position)
    {
        switch (map[position])
        {
            case Cell.Empty:
            case Cell.DoorRedOpen:
            case Cell.DoorGreenOpen:
            case Cell.DoorBlueOpen:
                // Nothing special, just leave
                break;

            case Cell.PressurePlateRed:
                CloseAllDoors(Cell.DoorRedOpen);
                break;
            case Cell.PressurePlateGreen:
                CloseAllDoors(Cell.DoorGreenOpen);
                break;
            case Cell.PressurePlateBlue:
                CloseAllDoors(Cell.DoorBlueOpen);
                break;

            default:
                throw new NotImplementedException($"Cell {map[position]} at position {position} cannot be left"); // Should not be possible
        }
    }

    private void EnterCell(ref Player player, Position position)
    {
        switch (map[position])
        {
            case Cell.Empty:
            case Cell.DoorRedOpen:
            case Cell.DoorGreenOpen:
            case Cell.DoorBlueOpen:
                // Nothing special, just enter
                break;

            case Cell.PressurePlateRed:
                OpenAllDoors(Cell.DoorRedClosed);
                break;
            case Cell.PressurePlateGreen:
                OpenAllDoors(Cell.DoorGreenClosed);
                break;
            case Cell.PressurePlateBlue:
                OpenAllDoors(Cell.DoorBlueClosed);
                break;

            case Cell.Exit:
                ExitMap(ref player);
                break;

            case Cell.KeyRed:
            case Cell.KeyGreen:
            case Cell.KeyBlue:
            case Cell.Treasure:
                PickupInventory(ref player, position, Cell.Empty);
                break;

            case Cell.Sword:
                PickupSword(ref player, position);
                break;

            case Cell.Health:
                PickupHealth(ref player, position);
                break;

            default:
                // Should not be possible
                throw new NotImplementedException($"Cell {map[position]} at position {position} cannot be entered by {player.Id}");
        }
    }

    private void Use(ref Player player, Position usePos)
    {
        if (TryUseOnEnemyOrPlayer(player, usePos))
        {
            return;
        }

        switch (map[usePos])
        {
            case Cell.Exit:
            case Cell.Wall:
            case Cell.DoorRedOpen:
            case Cell.KeyRed:
            case Cell.DoorGreenOpen:
            case Cell.KeyGreen:
            case Cell.DoorBlueOpen:
            case Cell.KeyBlue:
            case Cell.Sword:
            case Cell.Health:
            case Cell.Treasure:
                // Cannot use on this
                throw new UseNotAllowedException();

            case Cell.Empty:
                PlaceBoulderOnEmpty(ref player, usePos, Cell.Boulder);
                break;
            case Cell.PressurePlateRed:
                PlaceBoulderOnPressurePlate(ref player, usePos, Cell.PressurePlateRedWithBoulder, Cell.DoorRedClosed);
                break;
            case Cell.PressurePlateGreen:
                PlaceBoulderOnPressurePlate(ref player, usePos, Cell.PressurePlateGreenWithBoulder, Cell.DoorGreenClosed);
                break;
            case Cell.PressurePlateBlue:
                PlaceBoulderOnPressurePlate(ref player, usePos, Cell.PressurePlateBlueWithBoulder, Cell.DoorBlueClosed);
                break;

            case Cell.Boulder:
                PickupInventory(ref player, usePos, Cell.Empty);
                break;
            case Cell.PressurePlateRedWithBoulder:
                PickupBoulderFromPressurePlate(ref player, usePos, Cell.PressurePlateRed, Cell.DoorRedOpen);
                break;
            case Cell.PressurePlateGreenWithBoulder:
                PickupBoulderFromPressurePlate(ref player, usePos, Cell.PressurePlateGreen, Cell.DoorGreenOpen);
                break;
            case Cell.PressurePlateBlueWithBoulder:
                PickupBoulderFromPressurePlate(ref player, usePos, Cell.PressurePlateBlue, Cell.DoorBlueOpen);
                break;

            case Cell.DoorRedClosed:
                UseKeyToOpenDoor(ref player, usePos, Inventory.KeyRed);
                break;
            case Cell.DoorGreenClosed:
                UseKeyToOpenDoor(ref player, usePos, Inventory.KeyGreen);
                break;
            case Cell.DoorBlueClosed:
                UseKeyToOpenDoor(ref player, usePos, Inventory.KeyBlue);
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }
    }

    private void PickupInventory(ref Player player, Position position, Cell emptyCellType)
    {
        // Cannot pickup if inventory is full
        if (player.Inventory != Inventory.None) throw new InventoryFullException();

        // Is it an item that can be picked up?
        var item = map[position].ToInventory();
        if (item == Inventory.None) throw new NotImplementedException(); // developer error

        // Put in inventory and remove from map
        player = player with { Inventory = item };
        map = map.Set(position, emptyCellType);

        lastChangeTick = ticks;
    }

    private void PickupSword(ref Player player, Position position)
    {
        // Cannot pickup if player already has sword
        if (player.HasSword) throw new InventoryFullException();

        // Add to player and remove from map
        player = player with { HasSword = true };
        map = map.Set(position, Cell.Empty);
        lastChangeTick = ticks;
    }

    private void PickupHealth(ref Player player, Position position)
    {
        // Add to player's health and remove from map
        player = player with { Health = player.Health + Parameters.ExtraHealth };
        map = map.Set(position, Cell.Empty);
        lastChangeTick = ticks;
    }

    private bool TryUseOnEnemyOrPlayer(Player player, Position usePos)
    {
        foreach (var character in GetPresentCharacters())
        {
            if (usePos.Equals(character.Position))
            {
                if (!player.HasSword) throw new NoSwordException();
                DealDamage(character.Id, 1);
                return true;
            }
        }
        return false;
    }

    private void PlaceBoulderOnEmpty(ref Player player, Position usePos, Cell placedCellType)
    {
        if (player.Inventory == Inventory.None) throw new InventoryEmptyException();
        if (player.Inventory != Inventory.Boulder) throw new UseNotAllowedException();

        player = player with { Inventory = Inventory.None };
        map = map.Set(usePos, placedCellType);
        lastChangeTick = ticks;
    }

    private void PlaceBoulderOnPressurePlate(ref Player player, Position usePos, Cell plateWithBoulderCell, Cell closedDoorCell)
    {
        if (player.Inventory == Inventory.None) throw new InventoryEmptyException();
        if (player.Inventory != Inventory.Boulder) throw new UseNotAllowedException();

        PlaceBoulderOnEmpty(ref player, usePos, plateWithBoulderCell);
        OpenAllDoors(closedDoorCell);
    }

    private void PickupBoulderFromPressurePlate(ref Player player, Position usePos, Cell plateCell, Cell openDoorCell)
    {
        PickupInventory(ref player, usePos, plateCell);
        CloseAllDoors(openDoorCell);
    }

    private void UseKeyToOpenDoor(ref Player player, Position usePosition, Inventory item)
    {
        // Cannot use if item is not in inventory
        if (player.Inventory == Inventory.None) throw new InventoryEmptyException();
        if (player.Inventory != item) throw new UseNotAllowedException();

        // Open all the doors of the same color
        var closedDoor = map[usePosition];
        OpenAllDoors(closedDoor);

        // Remove key from inventory
        player = player with { Inventory = Inventory.None };
        lastChangeTick = ticks;
    }

    private void OpenAllDoors(Cell closedDoor)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var pos = map.Pos(y, x);
                if (map[pos] == closedDoor)
                {
                    // If already requested to close, then simple revert that,
                    // otherwise add delayed open.
                    if (!doorsToClose.Remove(pos))
                    {
                        doorsToOpen.Add(pos);
                    }
                }
            }
        }
    }

    private void CloseAllDoors(Cell openDoor)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var pos = map.Pos(y, x);
                if (map[pos] == openDoor)
                {
                    // If already requested to open, then simple revert that,
                    // otherwise add delayed close.
                    if (!doorsToOpen.Remove(pos))
                    {
                        doorsToClose.Add(pos);
                    }
                }
            }
        }
    }

    private void ProcessDoors()
    {
        foreach (var pos in doorsToOpen)
        {
            var closedDoor = map[pos];
            var openDoor = closedDoor.ToOpenDoor();
            map = map.Set(pos, openDoor);
            lastChangeTick = ticks;
        }
        doorsToOpen.Clear();

        foreach (var pos in doorsToClose)
        {
            var openDoor = map[pos];
            var closedDoor = openDoor.ToClosedDoor();
            map = map.Set(pos, closedDoor);
            lastChangeTick = ticks;
            KillCharacterAtPosition(pos);
        }
        doorsToClose.Clear();
    }

    private void ExitMap(ref Player player)
    {
        var cannotExit =
            // Cannot carry boulder when exiting
            (player.Inventory == Inventory.Boulder) ||
            // In last level with boss, treasure is needed when exiting
            (map.IsFinal && player.Inventory != Inventory.Treasure);

        if (cannotExit)
        {
            // Kill it on the spot
            player = player with { Health = 0 };
        }
        else
        {
            // Remove player from game, with health intact
            player = player with { Position = Position.Invalid };
        }

        lastChangeTick = ticks;
    }

    private void KillCharacterAtPosition(Position pos)
    {
        foreach (var character in GetPresentCharacters())
        {
            if (pos.Equals(character.Position))
            {
                DealDamage(character.Id, character.Health);
            }
        }
    }

    private static bool AreAdjacent(Position a, Position b)
    {
        var dy = Math.Abs(a.y - b.y);
        var dx = Math.Abs(a.x - b.x);
        return (dy == 1 && dx == 0) || (dy == 0 && dx == 1);
    }

    private static bool AreAdjacent(GameCharacter a, GameCharacter b)
    {
        return a.IsPresent && b.IsPresent && AreAdjacent(a.Position, b.Position);
    }

    private void ProcessEnemy(Enemy enemy, Player? prevPlayer1, Player? prevPlayer2)
    {
        // Is this enemy still alive?
        if (!enemy.IsPresent || !enemy.IsAlive) return;

        if (!TryEnemyAttackAdjacentPlayer(ref enemy, prevPlayer1, prevPlayer2))
        {
            MoveEnemyToClosestPlayer(ref enemy);
        }

        map = map.SetCharacter(enemy);
    }

    private bool TryEnemyAttackAdjacentPlayer(ref Enemy enemy, Player? prevPlayer1, Player? prevPlayer2)
    {
        // Are there players adjacent to the enemy?
        var currentAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (map.Player1 != null && AreAdjacent(enemy, map.Player1)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player1);
        if (map.Player2 != null && AreAdjacent(enemy, map.Player2)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player2);

        // No, then cannot attack
        if (currentAdjacentPlayers.Count == 0) return false;

        // Are there players that were adjacent in the previous tick as well?
        var previousAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (prevPlayer1 != null && AreAdjacent(enemy, prevPlayer1)) previousAdjacentPlayers = previousAdjacentPlayers.Add(GameCharacterId.Player1);
        if (prevPlayer2 != null && AreAdjacent(enemy, prevPlayer2)) previousAdjacentPlayers = previousAdjacentPlayers.Add(GameCharacterId.Player2);
        var adjacentPlayers = currentAdjacentPlayers.Intersect(previousAdjacentPlayers);

        // Yes, then attack
        if (adjacentPlayers.Count > 0)
        {
            var adjacentPlayer = adjacentPlayers.PickOne(random);
            if (map.Player1 != null && adjacentPlayer == GameCharacterId.Player1) DealDamage(map.Player1, enemy.Damage);
            if (map.Player2 != null && adjacentPlayer == GameCharacterId.Player2) DealDamage(map.Player2, enemy.Damage);
        }

        // Return true, even when not attacked.
        // To prevent movement, since there are players adjacent.
        return true;
    }

    private void MoveEnemyToClosestPlayer(ref Enemy enemy)
    {
        var enemy_ = enemy;

        // Once in a while do not move
        if (random.Next(0, 100) < 10) return;

        // Make a list of players, ordered by distance from enemy
        ImmutableList<Player> players = [];
        if (map.Player1 != null && map.Player1.IsPresent) players = players.Add(map.Player1);
        if (map.Player2 != null && map.Player2.IsPresent) players = players.Add(map.Player2);
        var closestPlayers = players.
            OrderBy(p => p.Position.DistanceTo(enemy_.Position));

        // Are players visible to the enemy?
        var visiblePlayers = closestPlayers.Where(p => IsPlayerVisibleByEnemy(enemy_, p));
        if (visiblePlayers.Any())
        {
            // A player became visible to the enemy, activate the enemy
            enemy = enemy with { IsTriggered = true };

            // Let it chase the closest player
            var visiblePlayer = visiblePlayers.First();
            MoveEnemyTowards(ref enemy, visiblePlayer.Position);
        }
        else if (enemy.IsTriggered && closestPlayers.Any())
        {
            // No player visible, but it was before, so simply move towards the closest player
            // at a slower pace, by randomly skipping moves
            if (random.Next(0, 100) > 50)
            {
                var closestPlayer = closestPlayers.First();
                MoveEnemyTowards(ref enemy, closestPlayer.Position);
            }
        }
    }

    private bool IsPlayerVisibleByEnemy(Enemy enemy, Player player)
    {
        return map.IsVisible(enemy.Position, player.Position, Parameters.EnemyVisibilityRange);
    }

    private void MoveEnemyTowards(ref Enemy enemy, Position targetPosition)
    {
        // Dumb movement algorithm
        // Just check if enemy needs to move vertically and horizontally.
        // Pick one randomly.

        ImmutableList<Position> nextPositions = [];

        // Check vertical movement
        var dy = targetPosition.y - enemy.Position.y;
        if (Math.Abs(dy) > 0)
        {
            var nextPosY = map.Pos(enemy.Position.y + Math.Sign(dy), enemy.Position.x);
            if (CanMoveTo(nextPosY, isEnemy: true)) nextPositions = nextPositions.Add(nextPosY);
        }

        // Check horizontal movement
        var dx = targetPosition.x - enemy.Position.x;
        if (Math.Abs(dx) > 0)
        {
            var nextPosX = map.Pos(enemy.Position.y, enemy.Position.x + Math.Sign(dx));
            if (CanMoveTo(nextPosX, isEnemy: true)) nextPositions = nextPositions.Add(nextPosX);
        }

        // Take a random move towards the player
        if (nextPositions.Count > 0)
        {
            var nextPos = nextPositions.PickOne(random);
            enemy = enemy with { Position = nextPos };
        }
    }

    private void DealDamage<T>(T? character, int damage) where T : GameCharacter
    {
        if (character == null) return;
        character = character with { Health = Math.Max(0, character.Health - damage) };
        lastChangeTick = ticks;
        map = map.SetCharacter(character);
    }

    private void DealDamage(GameCharacterId characterId, int damage)
    {
        switch (characterId)
        {
            case GameCharacterId.Player1:
                DealDamage(map.Player1, damage);
                break;
            case GameCharacterId.Player2:
                DealDamage(map.Player2, damage);
                break;
            case GameCharacterId.Enemy1:
                DealDamage(map.Enemy1, damage);
                break;
            case GameCharacterId.Enemy2:
                DealDamage(map.Enemy2, damage);
                break;
            case GameCharacterId.Enemy3:
                DealDamage(map.Enemy3, damage);
                break;
        }
    }

    private bool CanMoveTo(Position position, bool isEnemy = false)
    {
        // Move within map bounds
        if (position.x < 0 || position.x >= map.Width) return false;
        if (position.y < 0 || position.y >= map.Height) return false;

        // Check collisions with players and enemies.
        foreach (var character in GetPresentCharacters())
        {
            if (position.Equals(character.Position)) return false;
        }

        // Check if cell is walkable
        // Enemies can only enter cells that appear empty
        var cell = map[position];
        return !isEnemy ? cell.CanWalkOn() : cell.IsEmpty();
    }

    private void CleanupDeadCharacters()
    {
        if (map.Player1 != null)
        {
            var player1 = map.Player1;
            CleanupDeadCharacter(ref player1);
            map = map.SetCharacter(player1);
        }
        if (map.Player2 != null)
        {
            var player2 = map.Player2;
            CleanupDeadCharacter(ref player2);
            map = map.SetCharacter(player2);
        }

        foreach (var enemy in map.Enemies)
        {
            var newEnemy = enemy;
            CleanupDeadCharacter(ref newEnemy);
            map = map.SetCharacter(newEnemy);
        }
    }

    private void CleanupDeadCharacter<T>(ref T character) where T : GameCharacter
    {
        // Make characters not present when they are no longer alive
        if (character.IsAlive || !character.IsPresent) return;

        var position = character.Position;

        // Remove character from game
        character = character with { Position = Position.Invalid };

        // Boss drops two treasures (most important otherwise cannot exit)
        if (character is Enemy enemy && enemy.IsBoss)
        {
            DropItem(position, Cell.Treasure);
            DropItem(position, Cell.Treasure);
        }
        // Drop inventory
        if (character.Inventory != Inventory.None)
        {
            var loot = character.Inventory.ToDroppedLoot();
            DropItem(position, loot);
            character = character with { Inventory = Inventory.None };
        }
        // Drop sword
        if (character.HasSword)
        {
            DropItem(position, Cell.Sword);
            character = character with { HasSword = false };
        }

        lastChangeTick = ticks;
    }

    private void DropItem(Position fromPosition, Cell item)
    {
        // Drop only on empty cell
        // This is to prevent overwriting a pressure plate
        var dropPos = fromPosition;
        if (IsOccupied(dropPos))
        {
            dropPos = FindEmptyPosAround(dropPos);
        }

        // If no position can be found, then simply not place the item and lose it.
        if (!dropPos.IsValid) return;

        Debug.Assert(map[dropPos] == Cell.Empty);
        map = map.Set(dropPos, item);
    }

    private bool IsOccupied(Position pos)
    {
        return !pos.IsValid ||
            map[pos] != Cell.Empty ||
            GetPresentCharacters().Any(c => c.Position.Equals(pos));
    }

    private Position FindEmptyPosAround(Position pos)
    {
        ImmutableList<Position> choices = [];
        void AddIfNotOccupied(Position p)
        {
            if (!IsOccupied(p)) choices = choices.Add(p);
        }

        AddIfNotOccupied(map.Pos(pos.y - 1, pos.x - 1));
        AddIfNotOccupied(map.Pos(pos.y - 1, pos.x));
        AddIfNotOccupied(map.Pos(pos.y - 1, pos.x + 1));
        AddIfNotOccupied(map.Pos(pos.y, pos.x - 1));
        AddIfNotOccupied(map.Pos(pos.y, pos.x + 1));
        AddIfNotOccupied(map.Pos(pos.y + 1, pos.x - 1));
        AddIfNotOccupied(map.Pos(pos.y + 1, pos.x));
        AddIfNotOccupied(map.Pos(pos.y + 1, pos.x + 1));

        if (choices.Count == 0) return Position.Invalid;

        // Pick one closest to a player
        return choices.OrderBy(p => DistanceToAnyPlayer(p)).First();
    }

    private double DistanceToAnyPlayer(Position p)
    {
        double distance = double.PositiveInfinity;
        if (map.Player1 != null && map.Player1.IsPresent)
        {
            var d1 = map.Player1.Position.DistanceTo(p);
            if (d1 < distance) distance = d1;
        }
        if (map.Player2 != null && map.Player2.IsPresent)
        {
            var d2 = map.Player2.Position.DistanceTo(p);
            if (d2 < distance) distance = d2;
        }
        return distance;
    }

    private static bool IsPlayerActive(Player? player, ImmutableList<Position> playerPositions)
    {
        // Only check players that are alive
        if (player == null || !player.IsPresent)
        {
            return false;
        }

        // If there was no activity at all, then player is inactive
        if (playerPositions.Count == 0)
        {
            return false;
        }

        if (playerPositions.Count == Parameters.MaxInactivityTicks)
        {
            // Check if min and max positions have changed at all
            var minY = playerPositions.Min(p => p.y);
            var maxY = playerPositions.Max(p => p.y);
            var minX = playerPositions.Min(p => p.x);
            var maxX = playerPositions.Max(p => p.x);
            if ((maxY - minY) < Parameters.MinIdleMoveDistance &&
                (maxX - minX) < Parameters.MinIdleMoveDistance)
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<GameCharacter> GetPresentCharacters()
    {
        foreach (var character in map.AllCharacters.Where(c => c.IsPresent))
        {
            yield return character;
        }
    }

    #region State

    private PlayerState GetPlayerState(Player player)
    {
        const int visibilityRange = Parameters.PlayerVisibilityRange;

        Tile[] surroundings = [];

        if (player.IsPresent)
        {
            var width = visibilityRange * 2 + 1;
            var height = visibilityRange * 2 + 1;

            surroundings = new Tile[height * width];

            var top = player.Position.y - visibilityRange;
            var left = player.Position.x - visibilityRange;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    surroundings[y * width + x] =
                        map.ToVisibleTile(map.Pos(top + y, left + x), player.Position, visibilityRange);
                }
            }
        }

        return new PlayerState((player.Position.y, player.Position.x), player.Health, player.Inventory, player.HasSword, surroundings);
    }

    #endregion
}
