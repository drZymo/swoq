using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

public class Game : IGame
{
    private readonly TimeSpan maxInactivityTime;

    private int ticks = 0;
    private int lastChangeTick = 0;

    private Map map;
    private Player? player1 = null;
    private ImmutableList<Position> player1Positions = [];
    private Player? player2 = null;
    private ImmutableList<Position> player2Positions = [];
    private ImmutableDictionary<GameCharacterId, Enemy> enemies = ImmutableDictionary<GameCharacterId, Enemy>.Empty;

    public Game(Map map, TimeSpan maxInactivityTime)
    {
        this.map = map;
        this.maxInactivityTime = maxInactivityTime;

        player1 = new Player(GameCharacterId.Player1, map.InitialPlayer1Position);
        if (map.InitialPlayer2Position.HasValue)
        {
            player2 = new Player(GameCharacterId.Player2, map.InitialPlayer2Position.Value);
        }
        if (map.InitialEnemy1Position.HasValue)
        {
            if (map.IsEnemy1Boss)
            {
                enemies = enemies.Add(GameCharacterId.Boss, new Enemy(GameCharacterId.Boss,
                    map.InitialEnemy1Position.Value,
                    Inventory: map.InitialEnemy1Inventory,
                    Health: Parameters.BossHealth,
                    Damage: Parameters.BossDamage,
                    HasSword: false));
            }
            else
            {
                enemies = enemies.Add(GameCharacterId.Enemy1, new Enemy(GameCharacterId.Enemy1, map.InitialEnemy1Position.Value, Inventory: map.InitialEnemy1Inventory));
            }
        }
        if (map.InitialEnemy2Position.HasValue)
        {
            enemies = enemies.Add(GameCharacterId.Enemy2, new Enemy(GameCharacterId.Enemy2, map.InitialEnemy2Position.Value, Inventory: map.InitialEnemy2Inventory));
        }
        if (map.InitialEnemy3Position.HasValue)
        {
            enemies = enemies.Add(GameCharacterId.Enemy3, new Enemy(GameCharacterId.Enemy3, map.InitialEnemy3Position.Value, Inventory: map.InitialEnemy3Inventory));
        }
    }

    public Guid Id { get; } = Guid.NewGuid();
    public GameState State => CreateState();
    public DateTime LastActionTime { get; private set; } = Clock.Now;

    public bool IsFinished { get; private set; } = false;

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        var prevPlayer1 = player1;
        var prevPlayer2 = player2;

        if (IsFinished) throw new GameFinishedException(CreateState());

        if (!CheckIsActive())
        {
            IsFinished = true;
            throw new NoProgressException(CreateState());
        }

        Debug.Assert(player1 != null || player2 != null);

        // Pre conditions
        if (action1 != null)
        {
            if (player1 == null || !player1.Position.IsValid()) throw new Player1NotPresentException(CreateState());
            if (player1.Health <= 0) throw new Player1DiedException(CreateState());
        }
        if (action2 != null)
        {
            if (player2 == null || !player2.Position.IsValid()) throw new Player2NotPresentException(CreateState());
            if (player2.Health <= 0) throw new Player2DiedException(CreateState());
        }

        PerformPlayerAction(action1, ref player1);
        PerformPlayerAction(action2, ref player2);

        // Process enemies using previous positions of players
        foreach (var enemy in enemies.Values)
        {
            var newEnemy = enemy;
            ProcessEnemy(ref newEnemy, prevPlayer1, prevPlayer2);
            enemies = enemies.SetItem(newEnemy.Id, newEnemy);
        }

        CleanupDeadCharacters();

        // Store current positions
        StorePlayerPosition(player1, ref player1Positions);
        StorePlayerPosition(player2, ref player2Positions);

        UpdateGameStatus();

        LastActionTime = Clock.Now;
        ticks++;
    }

    public bool CheckIsActive()
    {
        if ((Clock.Now - LastActionTime) > maxInactivityTime)
        {
            return false;
        }

        if (player1Positions.Count > 0 || player2Positions.Count > 0)
        {
            var player1Active = IsPlayerActive(player1, player1Positions);
            var player2Active = IsPlayerActive(player2, player2Positions);
            if (!player1Active && !player2Active)
            {
                return false;
            }
        }

        return true;
    }

    private GameState CreateState()
    {
        Debug.Assert(player1 != null || player2 != null);
        PlayerState? player1State = player1 != null ? GetPlayerState(player1) : null;
        PlayerState? player2State = player2 != null ? GetPlayerState(player2) : null;
        return new GameState(ticks, map.Level, IsFinished, player1State, player2State);
    }

    private void UpdateGameStatus()
    {
        Debug.Assert(!IsFinished);

        if (player1 != null && player1.Health <= 0)
        {
            IsFinished = true;
            throw new Player1DiedException(CreateState());
        }
        if (player2 != null && player2.Health <= 0)
        {
            IsFinished = true;
            throw new Player2DiedException(CreateState());
        }
        if ((ticks - lastChangeTick) > Parameters.MaxNoProgressTicks)
        {
            // Time since last change was too long ago
            IsFinished = true;
            throw new NoProgressException(CreateState());
        }
        if ((player1 == null || (!player1.Position.IsValid() && player1.Health > 0)) &&
            (player2 == null || (!player2.Position.IsValid() && player2.Health > 0)))
        {
            // Both players exited the map alive
            IsFinished = true;
            // This is expected, so no exception
        }
    }

    private static void StorePlayerPosition(Player? player, ref ImmutableList<Position> playerPositions)
    {
        // Only store positions of active players
        if (player == null || !player.Position.IsValid()) return;

        // Store current position
        playerPositions = playerPositions.Add(player.Position);
        // Cleanup old entries
        while (playerPositions.Count > Parameters.MaxInactivityTicks)
        {
            playerPositions = playerPositions.RemoveAt(0);
        }
    }

    private void PerformPlayerAction(DirectedAction? action, ref Player? player)
    {
        if (action.HasValue && player != null)
        {
            var actionPos = GetDirectedActionPosition(player, action.Value);

            switch (action.Value)
            {
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
                    throw new UnknownActionException(CreateState());
            }
        }
    }

    private Position GetDirectedActionPosition(Player player, DirectedAction action) => action switch
    {
        DirectedAction.MoveNorth => (player.Position.y - 1, player.Position.x),
        DirectedAction.UseNorth => (player.Position.y - 1, player.Position.x),

        DirectedAction.MoveEast => (player.Position.y, player.Position.x + 1),
        DirectedAction.UseEast => (player.Position.y, player.Position.x + 1),

        DirectedAction.MoveSouth => (player.Position.y + 1, player.Position.x),
        DirectedAction.UseSouth => (player.Position.y + 1, player.Position.x),

        DirectedAction.MoveWest => (player.Position.y, player.Position.x - 1),
        DirectedAction.UseWest => (player.Position.y, player.Position.x - 1),

        _ => throw new UnknownActionException(CreateState()),
    };

    private void Move(ref Player player, Position movePos)
    {
        if (!CanMoveTo(movePos))
        {
            throw new MoveNotAllowedException(CreateState());
        }

        LeaveCell(player.Position);

        player = player with { Position = movePos };
        Debug.Assert(map[player.Position].CanWalkOn());

        EnterCell(ref player, player.Position);
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
                throw new NotImplementedException(); // Should not be possible
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
                throw new NotImplementedException(); // Should not be possible
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
                throw new UseNotAllowedException(CreateState());

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
        if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());

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
        if (player.HasSword) throw new InventoryFullException(CreateState());

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
        foreach (var character in GetAliveCharacters())
        {
            if (usePos.Equals(character.Position))
            {
                if (!player.HasSword) throw new NoSwordException(CreateState());
                DealDamage(character.Id, 1);
                return true;
            }
        }
        return false;
    }

    private void PlaceBoulderOnEmpty(ref Player player, Position usePos, Cell placedCellType)
    {
        if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());
        if (player.Inventory != Inventory.Boulder) throw new UseNotAllowedException(CreateState());

        player = player with { Inventory = Inventory.None };
        map = map.Set(usePos, placedCellType);
        lastChangeTick = ticks;
    }

    private void PlaceBoulderOnPressurePlate(ref Player player, Position usePos, Cell plateWithBoulderCell, Cell closedDoorCell)
    {
        if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());
        if (player.Inventory != Inventory.Boulder) throw new UseNotAllowedException(CreateState());

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
        if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());
        if (player.Inventory != item) throw new UseNotAllowedException(CreateState());

        // Open all the doors of the same color
        var closedDoor = map[usePosition];
        OpenAllDoors(closedDoor);

        // Remove key from inventory
        player = player with { Inventory = Inventory.None };
        lastChangeTick = ticks;
    }

    private void OpenAllDoors(Cell closedDoor)
    {
        var openDoor = closedDoor.ToOpenDoor();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[y, x] == closedDoor)
                {
                    map = map.Set(y, x, openDoor);
                    lastChangeTick = ticks;
                }
            }
        }
    }

    private void CloseAllDoors(Cell openDoor)
    {
        var closedDoor = openDoor.ToClosedDoor();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Position pos = (y, x);
                if (map[pos] == openDoor)
                {
                    map = map.Set(pos, closedDoor);
                    lastChangeTick = ticks;
                    KillCharacterAtPosition(pos);
                }
            }
        }
    }

    private void ExitMap(ref Player player)
    {
        var cannotExit =
            // Cannot carry boulder when exiting
            (player.Inventory == Inventory.Boulder) ||
            // In last level, treasure is needed when exiting
            (map.Level == Parameters.FinalLevel && player.Inventory != Inventory.Treasure);

        if (cannotExit)
        {
            player = player with { Health = 0 };
            CleanupDeadCharacter(ref player);
            return;
        }

        // Remove player from game, with health intact
        player = player with { Position = PositionEx.Invalid };
    }

    private void KillCharacterAtPosition(Position pos)
    {
        foreach (var character in GetAliveCharacters())
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
        return a.Position.IsValid() && b.Position.IsValid() &&
            AreAdjacent(a.Position, b.Position);
    }

    private void ProcessEnemy(ref Enemy enemy, Player? prevPlayer1, Player? prevPlayer2)
    {
        // Is this enemy still alive?
        if (!enemy.Position.IsValid()) return;
        if (enemy.Health <= 0) return;

        if (!TryEnemyAttackAdjacentPlayer(ref enemy, prevPlayer1, prevPlayer2))
        {
            MoveEnemyToClosestPlayer(ref enemy);
        }
    }

    private bool TryEnemyAttackAdjacentPlayer(ref Enemy enemy, Player? prevPlayer1, Player? prevPlayer2)
    {
        // Are there players adjacent to the enemy?
        var currentAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (player1 != null && AreAdjacent(enemy, player1)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player1);
        if (player2 != null && AreAdjacent(enemy, player2)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player2);

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
            var adjacentPlayer = adjacentPlayers.PickOne();
            if (player1 != null && adjacentPlayer == GameCharacterId.Player1) DealDamage(ref player1, enemy.Damage);
            if (player2 != null && adjacentPlayer == GameCharacterId.Player2) DealDamage(ref player2, enemy.Damage);
        }

        // Return true, even when not attacked.
        // To prevent movement, since there are players adjacent.
        return true;
    }

    private void MoveEnemyToClosestPlayer(ref Enemy enemy)
    {
        var enemy_ = enemy;

        // Once in a while do not move
        if (Rnd.Next(0, 100) < 10) return;

        // Make a list of players, ordered by distance from enemy
        ImmutableList<Player> players = [];
        if (player1 != null && player1.Position.IsValid()) players = players.Add(player1);
        if (player2 != null && player2.Position.IsValid()) players = players.Add(player2);
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
        else if (enemy.IsTriggered)
        {
            // No player visible, but it was before, so simply move towards the closest player
            // at a slower pace, by randomly skipping moves
            if (Rnd.Next(0, 100) > 50)
            {
                var closestPlayer = closestPlayers.First();
                MoveEnemyTowards(ref enemy, closestPlayer.Position);
            }
        }
    }

    private bool IsPlayerVisibleByEnemy(Enemy enemy, Player player)
    {
        var distance = enemy.Position.DistanceTo(player.Position);
        return distance < Parameters.EnemyVisibilityRange && IsVisible(enemy.Position, player.Position);
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
            var nextPosY = (enemy.Position.y + Math.Sign(dy), enemy.Position.x);
            if (CanMoveTo(nextPosY)) nextPositions = nextPositions.Add(nextPosY);
        }

        // Check horizontal movement
        var dx = targetPosition.x - enemy.Position.x;
        if (Math.Abs(dx) > 0)
        {
            var nextPosX = (enemy.Position.y, enemy.Position.x + Math.Sign(dx));
            if (CanMoveTo(nextPosX)) nextPositions = nextPositions.Add(nextPosX);
        }

        // Take a random move towards the player
        if (nextPositions.Count > 0)
        {
            var nextPos = nextPositions.PickOne();
            enemy = enemy with { Position = nextPos };
        }
    }

    private void DealDamage<T>(ref T? character, int damage) where T : GameCharacter
    {
        if (character == null) return;
        character = character with { Health = Math.Max(0, character.Health - damage) };
        lastChangeTick = ticks;
        character = CleanupDeadCharacter(ref character);
    }

    private void DealDamage(GameCharacterId characterId, int damage)
    {
        switch (characterId)
        {
            case GameCharacterId.Player1:
                DealDamage(ref player1, damage);
                break;
            case GameCharacterId.Player2:
                DealDamage(ref player2, damage);
                break;
            case GameCharacterId.Enemy1:
            case GameCharacterId.Enemy2:
            case GameCharacterId.Enemy3:
            case GameCharacterId.Boss:
                {
                    var newEnemy = enemies[characterId];
                    DealDamage(ref newEnemy, damage);
                    if (newEnemy != null)
                    {
                        enemies = enemies.SetItem(newEnemy.Id, newEnemy);
                    }
                }
                break;
        }
    }

    private bool CanMoveTo(Position position)
    {
        // Move within map bounds
        if (position.x < 0 || position.x >= map.Width) return false;
        if (position.y < 0 || position.y >= map.Height) return false;

        // Check collisions with players and enemies.
        foreach (var character in GetAliveCharacters())
        {
            if (position.Equals(character.Position)) return false;
        }

        // Check if cell is walkable
        var cell = map[position];
        return cell.CanWalkOn();
    }

    private bool IsVisible(Position from, Position to)
    {
        if (to.y < 0 || to.y >= map.Height) return false;
        if (to.x < 0 || to.x >= map.Width) return false;
        if (to.DistanceTo(from) > Parameters.PlayerVisibilityRange) return false;

        if (from.x < to.x && IsVisible(from.x + 0.5, from.y + 0.5, to.x, to.y + 0.5)) return true;
        if (from.x > to.x && IsVisible(from.x + 0.5, from.y + 0.5, to.x + 1, to.y + 0.5)) return true;
        if (from.y < to.y && IsVisible(from.x + 0.5, from.y + 0.5, to.x + 0.5, to.y)) return true;
        if (from.y > to.y && IsVisible(from.x + 0.5, from.y + 0.5, to.x + 0.5, to.y + 1)) return true;

        return false;
    }

    private bool IsVisible(double srcX, double srcY, double dstX, double dstY)
    {
        var dx = dstX - srcX;
        var dy = dstY - srcY;

        if (Math.Abs(dx) > 1e-6) // prevent division by small amount
        {
            var stepX = Math.Sign(dx);
            var x = stepX > 0 ? Math.Ceiling(srcX) : Math.Floor(srcX);
            var stepY = stepX * dy / dx;
            var y = srcY + (x - srcX) * dy / dx;

            while (!(srcX < dstX && x >= dstX) && !(srcX > dstX && x <= dstX))
            {
                var mapX = (int)(x + stepX * 0.5);
                var mapY = (int)y;
                if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height) break;
                if (!map[mapY, mapX].CanWalkOn()) return false; // Blocked by wall

                x += stepX;
                y += stepY;
            }
        }

        if (Math.Abs(dy) > 1e-6) // prevent division by small amount
        {
            var stepY = Math.Sign(dy);
            var y = stepY > 0 ? Math.Ceiling(srcY) : Math.Floor(srcY);
            var stepX = stepY * dx / dy;
            var x = srcX + (y - srcY) * dx / dy;

            while (!(srcY < dstY && y >= dstY) && !(srcY > dstY && y <= dstY))
            {
                var mapY = (int)(y + stepY * 0.5);
                var mapX = (int)x;
                if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height) break;
                if (!map[mapY, mapX].CanWalkOn()) return false; // Blocked by wall

                y += stepY;
                x += stepX;
            }
        }

        return true;
    }

    private void CleanupDeadCharacters()
    {
        if (player1 != null)
        {
            CleanupDeadCharacter(ref player1);
        }
        if (player2 != null)
        {
            CleanupDeadCharacter(ref player2);
        }

        foreach (var enemy in enemies.Values)
        {
            var newEnemy = enemy;
            CleanupDeadCharacter(ref newEnemy);
            enemies = enemies.SetItem(newEnemy.Id, newEnemy);
        }
    }

    private T CleanupDeadCharacter<T>(ref T character) where T : GameCharacter
    {
        if (character.Health <= 0 && character.Position.IsValid())
        {
            var position = character.Position;

            // Remove character from game
            character = character with { Position = PositionEx.Invalid };

            // Boss drops two treasures (most important otherwise cannot exit)
            if (character.Id == GameCharacterId.Boss)
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

        return character;
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
        if (!dropPos.IsValid()) return;

        Debug.Assert(map[dropPos] == Cell.Empty);
        map = map.Set(dropPos, item);
    }

    private bool IsOccupied(Position pos)
    {
        return !pos.IsValid() ||
            map[pos] != Cell.Empty ||
            GetAliveCharacters().Any(c => c.Position == pos);
    }

    private Position FindEmptyPosAround(Position pos)
    {
        ImmutableList<Position> choices = [];
        void AddIfNotOccupied(Position p)
        {
            if (!IsOccupied(p)) choices = choices.Add(p);
        }

        // First check positions adjacent
        AddIfNotOccupied((pos.y - 1, pos.x));
        AddIfNotOccupied((pos.y, pos.x - 1));
        AddIfNotOccupied((pos.y, pos.x + 1));
        AddIfNotOccupied((pos.y + 1, pos.x));
        if (choices.Count > 0) return choices.PickOne();

        // No empty positions found yet, try diagonal
        AddIfNotOccupied((pos.y - 1, pos.x - 1));
        AddIfNotOccupied((pos.y - 1, pos.x + 1));
        AddIfNotOccupied((pos.y + 1, pos.x - 1));
        AddIfNotOccupied((pos.y + 1, pos.x + 1));
        return choices.Count > 0 ? choices.PickOne() : PositionEx.Invalid;
    }

    private static bool IsPlayerActive(Player? player, ImmutableList<Position> playerPositions)
    {
        // Only check players that are alive
        if (player == null || !player.Position.IsValid())
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

    private IEnumerable<GameCharacter> GetAliveCharacters()
    {
        if (player1 != null && player1.Health > 0 && player1.Position.IsValid())
        {
            yield return player1;
        }
        if (player2 != null && player2.Health > 0 && player2.Position.IsValid())
        {
            yield return player2;
        }
        foreach (var enemy in enemies.Values)
        {
            if (enemy != null && enemy.Health > 0 && enemy.Position.IsValid())
            {
                yield return enemy;
            }
        }
    }

    #region State

    // TODO: Move to separate class?

    private PlayerState GetPlayerState(Player player)
    {
        Tile[] surroundings = [];

        if (player.Position.IsValid())
        {
            var width = Parameters.PlayerVisibilityRange * 2 + 1;
            var height = Parameters.PlayerVisibilityRange * 2 + 1;

            surroundings = new Tile[height * width];

            var top = player.Position.y - Parameters.PlayerVisibilityRange;
            var left = player.Position.x - Parameters.PlayerVisibilityRange;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    surroundings[y * width + x] = ToTile(player, (top + y, left + x));
                }
            }
        }

        return new PlayerState(player.Position, player.Health, player.Inventory, player.HasSword, surroundings);
    }

    private Tile ToTile(Player player, Position pos)
    {
        if (pos.Equals(player.Position))
        {
            return Tile.Player;
        }

        if (!IsVisible(player.Position, pos))
        {
            return Tile.Unknown;
        }

        foreach (var character in GetAliveCharacters())
        {
            if (pos.Equals(character.Position))
            {
                return character.Id switch
                {
                    GameCharacterId.Player1 => Tile.Player,
                    GameCharacterId.Player2 => Tile.Player,
                    GameCharacterId.Enemy1 => Tile.Enemy,
                    GameCharacterId.Enemy2 => Tile.Enemy,
                    GameCharacterId.Enemy3 => Tile.Enemy,
                    GameCharacterId.Boss => Tile.Boss,
                    _ => throw new NotImplementedException(),
                };
            }
        }

        return map[pos] switch
        {
            Cell.Unknown => Tile.Unknown,
            Cell.Empty => Tile.Empty,
            Cell.Wall => Tile.Wall,
            Cell.Exit => Tile.Exit,
            Cell.DoorRedClosed => Tile.DoorRed,
            Cell.KeyRed => Tile.KeyRed,
            Cell.DoorGreenClosed => Tile.DoorGreen,
            Cell.KeyGreen => Tile.KeyGreen,
            Cell.DoorBlueClosed => Tile.DoorBlue,
            Cell.KeyBlue => Tile.KeyBlue,
            Cell.PressurePlateRed => Tile.PressurePlateRed,
            Cell.PressurePlateGreen => Tile.PressurePlateGreen,
            Cell.PressurePlateBlue => Tile.PressurePlateBlue,
            Cell.Sword => Tile.Sword,
            Cell.Health => Tile.Health,
            Cell.Treasure => Tile.Treasure,

            Cell.Boulder => Tile.Boulder,
            Cell.PressurePlateRedWithBoulder => Tile.Boulder,
            Cell.PressurePlateGreenWithBoulder => Tile.Boulder,
            Cell.PressurePlateBlueWithBoulder => Tile.Boulder,

            // don't show open doors
            Cell.DoorRedOpen => Tile.Empty,
            Cell.DoorGreenOpen => Tile.Empty,
            Cell.DoorBlueOpen => Tile.Empty,
            _ => throw new NotImplementedException(),
        };
    }

    #endregion
}
