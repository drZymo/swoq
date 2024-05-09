using Swoq.Infra;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal enum GameStatus
{
    Active,
    Completed,
    Failed,
    Timeout
}

internal class Game : IGame
{
    private abstract record Character(string Name, Position Position, Inventory Inventory, int Health);
    private record Player(string Name, Position Position, Inventory Inventory = Inventory.None, int Health = Parameters.PlayerHealth, bool HasSword = false)
        : Character(Name, Position, Inventory, Health);
    private record Enemy(string Name, Position Position, Inventory Inventory = Inventory.None, int Health = Parameters.EnemyHealth)
        : Character(Name, Position, Inventory, Health);

    private int ticks = 0;
    private readonly int level;
    private Map map;

    private Player? player1 = null;
    private Player? player2 = null;
    private IImmutableList<Enemy> enemies = ImmutableList<Enemy>.Empty;

    private int lastChangeTick = 0;

    public Game(int level)
    {
        this.level = level;

        map = MapGenerator.Generate(level, Parameters.MapHeight, Parameters.MapWidth);
        player1 = new Player("Player1", map.InitialPlayer1Position);
        if (map.InitialPlayer2Position.HasValue)
        {
            player2 = new Player("Player2", map.InitialPlayer2Position.Value);
        }

        if (map.InitialEnemy1Position.HasValue)
        {
            enemies = enemies.Add(new Enemy("Enemy1", map.InitialEnemy1Position.Value, Inventory: map.InitialEnemy1Inventory));
        }
        if (map.InitialEnemy2Position.HasValue)
        {
            enemies = enemies.Add(new Enemy("Enemy2", map.InitialEnemy2Position.Value, Inventory: map.InitialEnemy2Inventory));
        }
    }

    public GameStatus Status { get; private set; } = GameStatus.Active;

    public Guid Id { get; } = Guid.NewGuid();
    public DateTime LastAction { get; private set; } = DateTime.Now;
    public GameState State => CreateState();

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (Status != GameStatus.Active) throw new GameFinishedException(CreateState());

        LastAction = DateTime.Now;
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

        if (action1 != null && player1 != null)
        {
            switch (action1.Action)
            {
                case Action.Move:
                    Move(ref player1, action1.Direction);
                    break;
                case Action.Use:
                    Use(ref player1, action1.Direction);
                    break;
                default:
                    throw new UnknownActionException(CreateState());
            }
        }

        if (action2 != null && player2 != null)
        {
            switch (action2.Action)
            {
                case Action.Move:
                    Move(ref player2, action2.Direction);
                    break;
                case Action.Use:
                    Use(ref player2, action2.Direction);
                    break;
                default:
                    throw new UnknownActionException(CreateState());
            }
        }

        foreach (var enemy in enemies)
        {
            var newEnemy = enemy;
            ProcessEnemy(ref newEnemy);
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
        return new GameState(ticks, level, Status != GameStatus.Active, player1State, player2State);
    }

    private void UpdateGameStatus()
    {
        Debug.Assert(Status == GameStatus.Active);

        if ((player1 != null && player1.Health <= 0) ||
            (player2 != null && player2.Health <= 0))
        {
            // One of the players died
            Status = GameStatus.Failed; // TODO: change to Died?
        }
        else if ((player1 == null || (!player1.Position.IsValid() && player1.Health > 0)) &&
            (player2 == null || (!player2.Position.IsValid() && player2.Health > 0)))
        {
            // Both players exited the map alive
            Status = GameStatus.Completed;
        }
        else if ((ticks - lastChangeTick) > Parameters.MaxIdleTicks)
        {
            Status = GameStatus.Timeout;
            Console.WriteLine($"Timeout. {ticks - lastChangeTick} ticks");
        }
    }

    private void Move(ref Player player, Direction direction)
    {
        var nextPos = GetDirectionPosition(player, direction);

        if (!CanMoveTo(nextPos))
        {
            throw new MoveNotAllowedException(CreateState());
        }

        LeaveCell(player.Position);

        player = player with { Position = nextPos };
        Debug.Assert(map[player.Position].CanWalkOn());

        EnterCell(ref player, player.Position);
    }

    private void Use(ref Player player, Direction direction)
    {
        var usePos = GetDirectionPosition(player, direction);

        if (TryUseOnEnemy(player, usePos))
        {
            // It was an enemy, do nothing more.
        }
        else
        {
            if (player.Inventory == Inventory.None) throw new InventoryEmptyException(CreateState());

            switch (map[usePos])
            {
                case Cell.Empty:
                case Cell.Exit:
                case Cell.Wall:
                case Cell.DoorRedOpen:
                case Cell.KeyRed:
                case Cell.DoorGreenOpen:
                case Cell.KeyGreen:
                case Cell.DoorBlueOpen:
                case Cell.KeyBlue:
                case Cell.Sword:
                    // Cannot use on this
                    throw new UseNotAllowedException(CreateState());

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
    }

    private Position GetDirectionPosition(Player player, Direction direction) => direction switch
    {
        Direction.North => (player.Position.y - 1, player.Position.x),
        Direction.East => (player.Position.y, player.Position.x + 1),
        Direction.South => (player.Position.y + 1, player.Position.x),
        Direction.West => (player.Position.y, player.Position.x - 1),
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
            case Cell.DoorBlackOpen:
                // Nothing special, just leave
                break;

            case Cell.PressurePlate:
                CloseAllDoors(Cell.DoorBlackOpen);
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
            case Cell.DoorBlackOpen:
                // Nothing special, just enter
                break;

            case Cell.PressurePlate:
                OpenAllDoors(Cell.DoorBlackClosed);
                break;

            case Cell.Exit:
                // Reomve player from game
                player = player with { Position = PositionEx.Invalid };
                break;

            case Cell.KeyRed:
            case Cell.KeyGreen:
            case Cell.KeyBlue:
                PickupKey(ref player, position);
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

    private void PickupKey(ref Player player, Position position)
    {
        // Cannot pickup if inventory is full
        if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());

        // Is it an item that can be picked up?
        var item = map[position].ToInventory();
        if (item == Inventory.None) throw new NotImplementedException(); // developer error

        // Put in inventory and remove from map
        player = player with { Inventory = item };
        map = map.Set(position, Cell.Empty);
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

    private bool TryUseOnEnemy(Player player, Position usePos)
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

    private void UseKeyToOpenDoor(ref Player player, Position usePosition, Inventory item)
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

    private void ProcessEnemy(ref Enemy enemy)
    {
        if (!enemy.Position.IsValid()) return;
        if (enemy.Health <= 0) return;

        // Is there a player near by?
        var closestPlayer = FindClosestVisiblePlayer(enemy.Position, Parameters.EnemyVisibilityRange);
        if (closestPlayer == null) return;

        // Attack if adjacent, chase otherwise
        var dy = closestPlayer.Position.y - enemy.Position.y;
        var dx = closestPlayer.Position.x - enemy.Position.x;
        if ((Math.Abs(dy) == 1 && dx == 0) || (dy == 0 && Math.Abs(dx) == 1))
        {
            // Attack
            if (ReferenceEquals(closestPlayer, player1)) DealDamage(ref player1, 1);
            if (ReferenceEquals(closestPlayer, player2)) DealDamage(ref player2, 1);
        }
        else
        {
            // Chase
            var nextPos = Math.Abs(dy) > Math.Abs(dx)
                ? (enemy.Position.y + Math.Sign(dy), enemy.Position.x)
                : (enemy.Position.y, enemy.Position.x + Math.Sign(dx));
            if (CanMoveTo(nextPos))
            {
                enemy = enemy with { Position = nextPos };
            }
        }
    }

    private Player? FindClosestVisiblePlayer(Position fromPos, int visibilityRange)
    {
        // Find closest visible player
        double minDist = double.PositiveInfinity;
        Player? closestPlayer = null;

        if (player1 != null)
        {
            var dist = player1.Position.DistanceTo(fromPos);
            if (dist <= visibilityRange &&
                IsVisible(fromPos, player1.Position) &&
                dist < minDist)
            {
                minDist = dist;
                closestPlayer = player1;
            }
        }

        if (player2 != null)
        {
            var dist = player2.Position.DistanceTo(fromPos);
            if (dist <= visibilityRange &&
                IsVisible(fromPos, player2.Position) &&
                dist < minDist)
            {
                closestPlayer = player2;
            }
        }

        return closestPlayer;
    }

    private void DealDamage<T>(ref T character, int damage) where T : Character
    {
        character = character with { Health = character.Health - damage };
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
        if (to.DistanceTo(from) >= Parameters.PlayerVisibilityRange) return false;

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

    private T CleanupDeadCharacter<T>(ref T character) where T : Character
    {
        if (character.Health <= 0 && character.Position.IsValid())
        {
            // Drop loot
            map = map.Set(character.Position, character.Inventory.ToDroppedLoot());
            character = character with { Inventory = Inventory.None };
            // Remove from game
            character = character with { Position = PositionEx.Invalid };
            lastChangeTick = ticks;
        }

        return character;
    }

    #region State

    // TODO: Move to separate class?

    private PlayerState GetPlayerState(Player player)
    {
        int[] surroundings = [];

        if (player.Position.IsValid())
        {
            var width = Parameters.PlayerVisibilityRange * 2 + 1;
            var height = Parameters.PlayerVisibilityRange * 2 + 1;

            surroundings = new int[height * width];

            var top = player.Position.y - Parameters.PlayerVisibilityRange;
            var left = player.Position.x - Parameters.PlayerVisibilityRange;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    surroundings[y * width + x] = ToCellState(player, (top + y, left + x));
                }
            }
        }

        return new PlayerState(player.Position, player.Health, ToInventoryState(player.Inventory), player.HasSword, surroundings);
    }

    private int ToCellState(Player player, Position pos)
    {
        const int UNKNOWN = 0;
        const int EMPTY = 1;
        const int PLAYER = 2;
        const int WALL = 3;
        const int EXIT = 4;
        const int DOOR_RED = 5;
        const int KEY_RED = 6;
        const int DOOR_GREEN = 7;
        const int KEY_GREEN = 8;
        const int DOOR_BLUE = 9;
        const int KEY_BLUE = 10;
        const int DOOR_BLACK = 11;
        const int PRESSURE_PLATE = 12;
        const int SWORD = 13;
        const int ENEMY = 14;
        const int HEALTH = 15;

        if (pos.Equals(player.Position))
        {
            return PLAYER;
        }

        if (IsVisible(player.Position, pos))
        {
            if ((player1 != null && pos.Equals(player1.Position)) ||
                (player2 != null && pos.Equals(player2.Position)))
            {
                return PLAYER;
            }

            foreach (var enemy in enemies)
            {
                if (pos.Equals(enemy.Position))
                {
                    return ENEMY;
                }
            }

            var cell = map[pos];
            switch (cell)
            {
                case Cell.Empty: return EMPTY;
                case Cell.Wall: return WALL;
                case Cell.Exit: return EXIT;
                case Cell.DoorRedClosed: return DOOR_RED;
                case Cell.KeyRed: return KEY_RED;
                case Cell.DoorGreenClosed: return DOOR_GREEN;
                case Cell.KeyGreen: return KEY_GREEN;
                case Cell.DoorBlueClosed: return DOOR_BLUE;
                case Cell.KeyBlue: return KEY_BLUE;
                case Cell.DoorBlackClosed: return DOOR_BLACK;
                case Cell.PressurePlate: return PRESSURE_PLATE;
                case Cell.Sword: return SWORD;
                case Cell.Health: return HEALTH;

                // don't show open doors
                case Cell.DoorRedOpen:
                case Cell.DoorGreenOpen:
                case Cell.DoorBlueOpen:
                case Cell.DoorBlackOpen:
                    return EMPTY;
            }
        }

        return UNKNOWN;
    }

    private static int ToInventoryState(Inventory inventory) => inventory switch
    {
        Inventory.None => 0,
        Inventory.KeyRed => 1,
        Inventory.KeyGreen => 2,
        Inventory.KeyBlue => 3,
        _ => throw new NotImplementedException(),
    };

    #endregion
}
