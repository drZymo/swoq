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
    private GamePlayer? player1 = null;
    private GamePlayer? player2 = null;
    private IImmutableList<GameEnemy> enemies = ImmutableList<GameEnemy>.Empty;

    public Game(Map map, TimeSpan maxInactivityTime)
    {
        this.map = map;
        this.maxInactivityTime = maxInactivityTime;

        player1 = new GamePlayer(GameCharacterId.Player1, map.InitialPlayer1Position);
        if (map.InitialPlayer2Position.HasValue)
        {
            player2 = new GamePlayer(GameCharacterId.Player2, map.InitialPlayer2Position.Value);
        }
        if (map.InitialEnemy1Position.HasValue)
        {
            if (map.IsEnemy1Boss)
            {
                enemies = enemies.Add(new GameEnemy(GameCharacterId.Boss,
                    map.InitialEnemy1Position.Value,
                    Inventory: map.InitialEnemy1Inventory,
                    Health: Parameters.BossHealth,
                    Damage: Parameters.BossDamage,
                    HasSword: false));
            }
            else
            {
                enemies = enemies.Add(new GameEnemy(GameCharacterId.Enemy1, map.InitialEnemy1Position.Value, Inventory: map.InitialEnemy1Inventory));
            }
        }
        if (map.InitialEnemy2Position.HasValue)
        {
            enemies = enemies.Add(new GameEnemy(GameCharacterId.Enemy2, map.InitialEnemy2Position.Value, Inventory: map.InitialEnemy2Inventory));
        }
        if (map.InitialEnemy3Position.HasValue)
        {
            enemies = enemies.Add(new GameEnemy(GameCharacterId.Enemy3, map.InitialEnemy3Position.Value, Inventory: map.InitialEnemy3Inventory));
        }
    }

    public Guid Id { get; } = Guid.NewGuid();
    public GameState State => CreateState();
    public DateTime LastActionTime { get; private set; } = Clock.Now;
    public bool IsInactive => (Clock.Now - LastActionTime) > maxInactivityTime;

    public bool IsFinished { get; private set; } = false;

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        var prevPlayer1 = player1;
        var prevPlayer2 = player2;

        if (IsFinished) throw new GameFinishedException(CreateState());

        if (IsInactive)
        {
            IsFinished = true;
            throw new GameTimeoutException(CreateState());
        }

        LastActionTime = Clock.Now;
        ticks++;

        Debug.Assert(player1 != null || player2 != null);

        // Pre conditions
        if (action1 != null)
        {
            if (player1 == null) throw new Player1NotPresentException(CreateState());
            if (!player1.Position.IsValid()) throw new Player1NotPresentException(CreateState());
            if (player1.Health <= 0) throw new Player1DiedException(CreateState());
        }
        if (action2 != null)
        {
            if (player2 == null) throw new Player2NotPresentException(CreateState());
            if (!player2.Position.IsValid()) throw new Player2NotPresentException(CreateState());
            if (player2.Health <= 0) throw new Player2DiedException(CreateState());
        }

        if (action1.HasValue && player1 != null)
        {
            var actionPos = GetDirectedActionPosition(player1, action1.Value);

            switch (action1.Value)
            {
                case DirectedAction.MoveNorth:
                case DirectedAction.MoveEast:
                case DirectedAction.MoveSouth:
                case DirectedAction.MoveWest:
                    Move(ref player1, actionPos);
                    break;
                case DirectedAction.UseNorth:
                case DirectedAction.UseEast:
                case DirectedAction.UseSouth:
                case DirectedAction.UseWest:
                    Use(ref player1, actionPos);
                    break;
                default:
                    throw new UnknownActionException(CreateState());
            }
        }

        if (action2.HasValue && player2 != null)
        {
            var actionPos = GetDirectedActionPosition(player2, action2.Value);

            switch (action2.Value)
            {
                case DirectedAction.MoveNorth:
                case DirectedAction.MoveEast:
                case DirectedAction.MoveSouth:
                case DirectedAction.MoveWest:
                    Move(ref player2, actionPos);
                    break;
                case DirectedAction.UseNorth:
                case DirectedAction.UseEast:
                case DirectedAction.UseSouth:
                case DirectedAction.UseWest:
                    Use(ref player2, actionPos);
                    break;
                default:
                    throw new UnknownActionException(CreateState());
            }
        }

        foreach (var enemy in enemies)
        {
            var newEnemy = enemy;
            ProcessEnemy(ref newEnemy, prevPlayer1, prevPlayer2);
            enemies = enemies.Replace(enemy, newEnemy);
        }

        // Cleanup dead characters
        CleanupDeadCharacters();

        UpdateGameStatus();
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
        else if (player2 != null && player2.Health <= 0)
        {
            IsFinished = true;
            throw new Player2DiedException(CreateState());
        }
        else if ((ticks - lastChangeTick) > Parameters.MaxIdleTicks)
        {
            // Time since last change was too long ago
            IsFinished = true;
            throw new NoProgressException(CreateState());
        }
        else if ((player1 == null || (!player1.Position.IsValid() && player1.Health > 0)) &&
            (player2 == null || (!player2.Position.IsValid() && player2.Health > 0)))
        {
            // Both players exited the map alive
            IsFinished = true;
            // This is expected, so no exception
        }
    }

    private void Move(ref GamePlayer player, Position movePos)
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

    private void Use(ref GamePlayer player, Position usePos)
    {
        if (TryUseOnEnemy(player, usePos))
        {
            // It was an enemy, do nothing more.
        }
        else
        {
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
                    if (player.Inventory != Inventory.Boulder) throw new InventoryEmptyException(CreateState());
                    PlaceBoulder(ref player, usePos, Cell.Boulder);
                    break;

                case Cell.PressurePlateRed:
                    if (player.Inventory != Inventory.Boulder) throw new InventoryEmptyException(CreateState());
                    PlaceBoulder(ref player, usePos, Cell.PressurePlateRedWithBoulder);
                    OpenAllDoors(Cell.DoorRedClosed);
                    break;
                case Cell.PressurePlateGreen:
                    if (player.Inventory != Inventory.Boulder) throw new InventoryEmptyException(CreateState());
                    PlaceBoulder(ref player, usePos, Cell.PressurePlateGreenWithBoulder);
                    OpenAllDoors(Cell.DoorGreenClosed);
                    break;
                case Cell.PressurePlateBlue:
                    if (player.Inventory != Inventory.Boulder) throw new InventoryEmptyException(CreateState());
                    PlaceBoulder(ref player, usePos, Cell.PressurePlateBlueWithBoulder);
                    OpenAllDoors(Cell.DoorBlueClosed);
                    break;

                case Cell.Boulder:
                    if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());
                    PickupInventory(ref player, usePos, Cell.Empty);
                    break;
                case Cell.PressurePlateRedWithBoulder:
                    if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());
                    PickupInventory(ref player, usePos, Cell.PressurePlateRed);
                    CloseAllDoors(Cell.DoorRedOpen);
                    break;
                case Cell.PressurePlateGreenWithBoulder:
                    if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());
                    PickupInventory(ref player, usePos, Cell.PressurePlateGreen);
                    CloseAllDoors(Cell.DoorGreenOpen);
                    break;
                case Cell.PressurePlateBlueWithBoulder:
                    if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());
                    PickupInventory(ref player, usePos, Cell.PressurePlateBlue);
                    CloseAllDoors(Cell.DoorBlueOpen);
                    break;

                case Cell.DoorRedClosed:
                    if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());
                    UseKeyToOpenDoor(ref player, usePos, Inventory.KeyRed);
                    break;
                case Cell.DoorGreenClosed:
                    if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());
                    UseKeyToOpenDoor(ref player, usePos, Inventory.KeyGreen);
                    break;
                case Cell.DoorBlueClosed:
                    if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());
                    UseKeyToOpenDoor(ref player, usePos, Inventory.KeyBlue);
                    break;

                default:
                    throw new NotImplementedException(); // Should not be possible
            }
        }
    }

    private Position GetDirectedActionPosition(GamePlayer player, DirectedAction action) => action switch
    {
        DirectedAction.MoveNorth => (player.Position.y - 1, player.Position.x),
        DirectedAction.UseNorth => (player.Position.y - 1, player.Position.x),

        DirectedAction.MoveEast => (player.Position.y, player.Position.x + 1),
        DirectedAction.UseEast => (player.Position.y, player.Position.x + 1),

        DirectedAction.MoveSouth => (player.Position.y + 1, player.Position.x),
        DirectedAction.UseSouth => (player.Position.y + 1, player.Position.x),

        DirectedAction.MoveWest => (player.Position.y, player.Position.x - 1),
        DirectedAction.UseWest => (player.Position.y, player.Position.x - 1),

        _ => throw new UnknownDirectionException(CreateState()),
    };

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

    private void EnterCell(ref GamePlayer player, Position position)
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

    private void PickupInventory(ref GamePlayer player, Position position, Cell emptyCellType)
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

    private void PickupSword(ref GamePlayer player, Position position)
    {
        // Cannot pickup if player already has sword
        if (player.HasSword) throw new InventoryFullException(CreateState());

        // Add to player and remove from map
        player = player with { HasSword = true };
        map = map.Set(position, Cell.Empty);
        lastChangeTick = ticks;
    }

    private void PickupHealth(ref GamePlayer player, Position position)
    {
        // Add to player's health and remove from map
        player = player with { Health = player.Health + Parameters.ExtraHealth };
        map = map.Set(position, Cell.Empty);
        lastChangeTick = ticks;
    }

    private bool TryUseOnEnemy(GamePlayer player, Position usePos)
    {
        foreach (var enemy in enemies)
        {
            if (usePos.Equals(enemy.Position))
            {
                if (!player.HasSword) throw new NoSwordException(CreateState());

                var newEnemy = enemy;
                DealDamage(ref newEnemy, 1);
                enemies = enemies.Replace(enemy, newEnemy);
                return true;
            }
        }

        return false;
    }

    private void PlaceBoulder(ref GamePlayer player, Position usePos, Cell placedCellType)
    {
        if (player.Inventory != Inventory.Boulder) throw new UseNotAllowedException(CreateState());

        player = player with { Inventory = Inventory.None };
        map = map.Set(usePos, placedCellType);
        lastChangeTick = ticks;
    }

    private void UseKeyToOpenDoor(ref GamePlayer player, Position usePosition, Inventory item)
    {
        // Cannot use if item is not in inventory
        if (player.Inventory != item) throw new UseNotAllowedException(CreateState());

        // Oopen all the doors of the same type
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

    private void ExitMap(ref GamePlayer player)
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
        if (player1 != null && player1.Position.IsValid() && pos.Equals(player1.Position))
        {
            DealDamage(ref player1, player1.Health);
        }
        if (player2 != null && player2.Position.IsValid() && pos.Equals(player2.Position))
        {
            DealDamage(ref player2, player2.Health);
        }
        foreach (var enemy in enemies)
        {
            var newEnemy = enemy;
            if (newEnemy.Position.IsValid() && pos.Equals(newEnemy.Position))
            {
                DealDamage(ref newEnemy, newEnemy.Health);
                enemies = enemies.Replace(enemy, newEnemy);
            }
        }
    }

    private static bool AreAdjacent(Position a, Position b)
    {
        var dy = Math.Abs(a.y - b.y);
        var dx = Math.Abs(a.x - b.x);
        return (dy == 1 && dx == 0) || (dy == 0 && dx == 1);
    }

    private static bool AraAdjacent(GameCharacter a, GameCharacter b)
    {
        return a.Position.IsValid() && b.Position.IsValid() &&
            AreAdjacent(a.Position, b.Position);
    }

    private void ProcessEnemy(ref GameEnemy enemy, GamePlayer? prevPlayer1, GamePlayer? prevPlayer2)
    {
        // Is this enemy still alive?
        if (!enemy.Position.IsValid()) return;
        if (enemy.Health <= 0) return;

        if (!TryEnemyAttackAdjacentPlayer(ref enemy, prevPlayer1, prevPlayer2))
        {
            MoveEnemyToClosestPlayer(ref enemy);
        }
    }

    private bool TryEnemyAttackAdjacentPlayer(ref GameEnemy enemy, GamePlayer? prevPlayer1, GamePlayer? prevPlayer2)
    {
        // Are there players adjacent to the enemy?
        var currentAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (player1 != null && AraAdjacent(enemy, player1)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player1);
        if (player2 != null && AraAdjacent(enemy, player2)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player2);

        // No, then cannot attack
        if (currentAdjacentPlayers.Count == 0) return false;

        // Are there players that were adjacent in the previous tick as well?
        var previousAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (prevPlayer1 != null && AraAdjacent(enemy, prevPlayer1)) previousAdjacentPlayers = previousAdjacentPlayers.Add(GameCharacterId.Player1);
        if (prevPlayer2 != null && AraAdjacent(enemy, prevPlayer2)) previousAdjacentPlayers = previousAdjacentPlayers.Add(GameCharacterId.Player2);
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

    private void MoveEnemyToClosestPlayer(ref GameEnemy enemy)
    {
        var enemy_ = enemy;

        // Once in a while do not move
        if (Rnd.Next(0, 100) < 10) return;

        // Make a list of players, ordered by distance from enemy
        ImmutableList<GamePlayer> players = [];
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

    private bool IsPlayerVisibleByEnemy(GameEnemy enemy, GamePlayer player)
    {
        var distance = enemy.Position.DistanceTo(player.Position);
        return distance < Parameters.EnemyVisibilityRange && IsVisible(enemy.Position, player.Position);
    }

    private void MoveEnemyTowards(ref GameEnemy enemy, Position targetPosition)
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

    private void DealDamage<T>(ref T character, int damage) where T : GameCharacter
    {
        character = character with { Health = Math.Max(0, character.Health - damage) };
        lastChangeTick = ticks;
        character = CleanupDeadCharacter(ref character);
    }

    private bool CanMoveTo(Position position)
    {
        // Move within map bounds
        if (position.x < 0 || position.x >= map.Width) return false;
        if (position.y < 0 || position.y >= map.Height) return false;

        // Check collisions with players and enemies.
        if (player1 != null && player1.Position.IsValid() && position.Equals(player1.Position)) return false;
        if (player2 != null && player2.Position.IsValid() && position.Equals(player2.Position)) return false;

        foreach (var enemy in enemies)
        {
            if (enemy.Position.IsValid() && position.Equals(enemy.Position)) return false;
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

        foreach (var enemy in enemies)
        {
            var newEnemy = enemy;
            CleanupDeadCharacter(ref newEnemy);
            enemies = enemies.Replace(enemy, newEnemy);
        }
    }

    private T CleanupDeadCharacter<T>(ref T character) where T : GameCharacter
    {
        if (character.Health <= 0 && character.Position.IsValid())
        {
            var position = character.Position;

            // Remove character from game
            character = character with { Position = PositionEx.Invalid };

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
            // Boss drops two treasures
            if (character.Id == GameCharacterId.Boss)
            {
                DropItem(position, Cell.Treasure);
                DropItem(position, Cell.Treasure);
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
            (player1 != null && player1.Position == pos) ||
            (player2 != null && player2.Position == pos) ||
            enemies.Any(e => e.Position.IsValid() && e.Position == pos);
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

    #region State

    // TODO: Move to separate class?

    private PlayerState GetPlayerState(GamePlayer player)
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

    private Tile ToTile(GamePlayer player, Position pos)
    {
        if (pos.Equals(player.Position))
        {
            return Tile.Player;
        }

        if (IsVisible(player.Position, pos))
        {
            if ((player1 != null && pos.Equals(player1.Position)) ||
                (player2 != null && pos.Equals(player2.Position)))
            {
                return Tile.Player;
            }

            foreach (var enemy in enemies)
            {
                if (pos.Equals(enemy.Position))
                {
                    return enemy.Id == GameCharacterId.Boss ? Tile.Boss : Tile.Enemy;
                }
            }

            var cell = map[pos];
            switch (cell)
            {
                case Cell.Empty: return Tile.Empty;
                case Cell.Wall: return Tile.Wall;
                case Cell.Exit: return Tile.Exit;
                case Cell.DoorRedClosed: return Tile.DoorRed;
                case Cell.KeyRed: return Tile.KeyRed;
                case Cell.DoorGreenClosed: return Tile.DoorGreen;
                case Cell.KeyGreen: return Tile.KeyGreen;
                case Cell.DoorBlueClosed: return Tile.DoorBlue;
                case Cell.KeyBlue: return Tile.KeyBlue;
                case Cell.PressurePlateRed: return Tile.PressurePlateRed;
                case Cell.PressurePlateGreen: return Tile.PressurePlateGreen;
                case Cell.PressurePlateBlue: return Tile.PressurePlateBlue;
                case Cell.Sword: return Tile.Sword;
                case Cell.Health: return Tile.Health;
                case Cell.Treasure: return Tile.Treasure;

                case Cell.Boulder:
                case Cell.PressurePlateRedWithBoulder:
                case Cell.PressurePlateGreenWithBoulder:
                case Cell.PressurePlateBlueWithBoulder:
                    return Tile.Boulder;

                // don't show open doors
                case Cell.DoorRedOpen:
                case Cell.DoorGreenOpen:
                case Cell.DoorBlueOpen:
                    return Tile.Empty;
            }
        }

        return Tile.Unknown;
    }

    #endregion
}
