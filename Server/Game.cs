using Swoq.Infra;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    private readonly Map map;

    private record Player(int Index, Position Position, Inventory Inventory = Inventory.None, int Health = 5, bool HasSword = false);
    private record Enemy(int Index, Position Position, Inventory Inventory = Inventory.None, int Health = 6); // TODO: merge with Player ?

    private Player player1;
    private Player? player2 = null;
    private Enemy? enemy1 = null;
    private Enemy? enemy2 = null;

    private bool isFinished = false;

    public Game(int level)
    {
        map = MapGenerator.Generate(level, Parameters.MapHeight, Parameters.MapWidth);
        player1 = new Player(1, map.InitialPlayer1Position);
        if (map.InitialPlayer2Position.HasValue)
        {
            player2 = new Player(2, map.InitialPlayer2Position.Value);
        }
        if (map.InitialEnemy1Position.HasValue)
        {
            enemy1 = new Enemy(1, map.InitialEnemy1Position.Value, Inventory: map.InitialEnemy1Inventory);
        }
        if (map.InitialEnemy2Position.HasValue)
        {
            enemy2 = new Enemy(2, map.InitialEnemy2Position.Value, Inventory: map.InitialEnemy2Inventory);
        }
    }

    public Guid Id { get; } = Guid.NewGuid();
    public int Width => map.Width;
    public int Height => map.Height;

    public GameState GetState()
    {
        PlayerState player1State = GetPlayerState(player1);
        PlayerState? player2State = player2 != null ? GetPlayerState(player2) : null;
        return new GameState(player1State, player2State, isFinished);
    }

    public void Act(DirectedAction action1, DirectedAction? action2 = null)
    {
        if (action2 != null && player2 == null) throw new Player2NotPresentException();

        switch (action1.Action)
        {
            case Action.Move:
                Move(ref player1, action1.Direction);
                break;
            case Action.Use:
                Use(ref player1, action1.Direction);
                break;
            default:
                throw new UnknownActionException();
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
                    throw new UnknownActionException();
            }
        }

        if (enemy1 != null)
        {
            ImmutableHashSet<int> attackables = ImmutableHashSet<int>.Empty;

            if (TryGetPlayerIndex((enemy1.Position.y - 1, enemy1.Position.x), out var playerNorth)) attackables = attackables.Add(playerNorth);
            if (TryGetPlayerIndex((enemy1.Position.y + 1, enemy1.Position.x), out var playerSouth)) attackables = attackables.Add(playerSouth);
            if (TryGetPlayerIndex((enemy1.Position.y, enemy1.Position.x - 1), out var playerWest)) attackables = attackables.Add(playerWest);
            if (TryGetPlayerIndex((enemy1.Position.y, enemy1.Position.x + 1), out var playerEast)) attackables = attackables.Add(playerEast);

            if (!attackables.IsEmpty)
            {
                var attackable = attackables.PickOne();
                if (attackable == 0) EnemyAttacksPlayer(ref player1);
                if (attackable == 1 && player2 != null) EnemyAttacksPlayer(ref player2);
            }
        }
    }

    private void EnemyAttacksPlayer(ref Player player)
    {
        Debug.Assert(player.Health > 0);

        player = player with { Health = player.Health - 1 };
        Console.WriteLine($"Player {player.Index} was attacked, health = {player.Health}");
        if (player.Health <= 0)
        {
            Console.WriteLine($"Player {player.Index} dies");
            // TODO: die
        }
    }

    private bool TryGetPlayerIndex(Position position, out int index)
    {
        if (position.Equals(player1.Position))
        {
            index = 0;
            return true;
        }

        if (player2 != null && position.Equals(player2.Position))
        {
            index = 1;
            return true;
        }

        index = default;
        return false;
    }

    private void Move(ref Player player, Direction direction)
    {
        if (isFinished) throw new GameFinishedException();

        var nextPos = GetDirectionPosition(player, direction);

        if (!CanMoveTo(nextPos))
        {
            throw new MoveNotAllowedException();
        }

        LeaveCell(player.Position);
        EnterCell(ref player, nextPos);

        player = player with { Position = nextPos };
        Debug.Assert(map[player.Position].CanWalkOn());
    }

    private void Use(ref Player player, Direction direction)
    {
        if (isFinished) throw new GameFinishedException();

        var usePos = GetDirectionPosition(player, direction);

        if (enemy1 != null && usePos.Equals(enemy1.Position))
        {
            if (!player.HasSword) throw new NoSwordException();

            enemy1 = enemy1 with { Health = enemy1.Health - 1 };
            Console.WriteLine($"Enemy {enemy1.Index} was attacked, health = {enemy1.Health}");
            if (enemy1.Health <= 0)
            {
                Console.WriteLine($"Enemy {enemy1.Index} dies");
                // TODO: die
            }
        }
        else if (enemy2 != null && usePos.Equals(enemy2.Position))
        {
            if (!player.HasSword) throw new NoSwordException();

            enemy2 = enemy2 with { Health = enemy2.Health - 1 };
            Console.WriteLine($"Enemy {enemy2.Index} was attacked, health = {enemy2.Health}");
            if (enemy2.Health <= 0)
            {
                Console.WriteLine($"Enemy {enemy2.Index} dies");
                // TODO: die
            }
        }
        else
        {
            if (player.Inventory == Inventory.None) throw new InventoryEmptyException();

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
                    throw new UseNotAllowedException();

                case Cell.DoorRedClosed:
                    UseItemOnClosedDoor(ref player, usePos, Inventory.KeyRed);
                    break;
                case Cell.DoorGreenClosed:
                    UseItemOnClosedDoor(ref player, usePos, Inventory.KeyGreen);
                    break;
                case Cell.DoorBlueClosed:
                    UseItemOnClosedDoor(ref player, usePos, Inventory.KeyBlue);
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
        _ => throw new UnknownDirectionException(),
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
                CloseBlackDoors();
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
                OpenBlackDoors();
                break;

            case Cell.Exit:
                // Game finished
                map[position] = Cell.Empty;
                isFinished = true;
                break;

            case Cell.KeyRed:
            case Cell.KeyGreen:
            case Cell.KeyBlue:
                PickupKey(ref player, position);
                break;

            case Cell.Sword:
                PickupSword(ref player, position);
                break;

            default:
                throw new NotImplementedException(); // Should not be possible
        }
    }

    private void PickupKey(ref Player player, Position position)
    {
        // Cannot pickup if inventory is full
        if (player.Inventory != Inventory.None) throw new InventoryFullException();

        // Is it an item that can be picked up?
        var item = ToInventory(map[position]);
        if (item == Inventory.None) throw new NotImplementedException(); // developer error

        // Put in inventory and remove from map
        player = player with { Inventory = item };
        map[position] = Cell.Empty;
    }

    private void PickupSword(ref Player player, Position position)
    {
        // Cannot pickup if player already has sword
        if (player.HasSword) throw new InventoryFullException();

        // Add to player and remove from map
        player = player with { HasSword = true };
        map[position] = Cell.Empty;
    }

    private void UseItemOnClosedDoor(ref Player player, Position position, Inventory item)
    {
        // Cannot use if item is not in inventory
        if (player.Inventory != item) throw new UseNotAllowedException();

        // Remove from inventory and change to open
        player = player with { Inventory = Inventory.None };

        // Open this and all adjacent door cells
        ImmutableQueue<Position> todo = [position];
        while (!todo.IsEmpty)
        {
            todo = todo.Dequeue(out var current);
            var doorType = map[current];
            map[current] = ToOpenDoor(doorType);

            if (map[current.y - 1, current.x] == doorType) todo = todo.Enqueue((current.y - 1, current.x));
            if (map[current.y + 1, current.x] == doorType) todo = todo.Enqueue((current.y + 1, current.x));
            if (map[current.y, current.x - 1] == doorType) todo = todo.Enqueue((current.y, current.x - 1));
            if (map[current.y, current.x + 1] == doorType) todo = todo.Enqueue((current.y, current.x + 1));
        }
    }

    private void OpenBlackDoors()
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[y, x] == Cell.DoorBlackClosed)
                {
                    map[y, x] = Cell.DoorBlackOpen;
                }
            }
        }
    }

    private void CloseBlackDoors()
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[y, x] == Cell.DoorBlackOpen)
                {
                    map[y, x] = Cell.DoorBlackClosed;
                }
            }
        }
    }

    private bool CanMoveTo(Position position)
    {
        if (position.x < 0 || position.x >= map.Width) return false;
        if (position.y < 0 || position.y >= map.Height) return false;

        var cell = map[position];

        return cell.CanWalkOn();
    }

    private bool IsVisible(Player player, Position pos)
    {
        if (pos.y < 0 || pos.y >= map.Height) return false;
        if (pos.x < 0 || pos.x >= map.Width) return false;
        if (pos.DistanceTo(player.Position) >= Parameters.PlayerVisibilityRange) return false;

        if (player.Position.x < pos.x && IsVisible(player.Position.x + 0.5, player.Position.y + 0.5, pos.x, pos.y + 0.5)) return true;
        if (player.Position.x > pos.x && IsVisible(player.Position.x + 0.5, player.Position.y + 0.5, pos.x + 1, pos.y + 0.5)) return true;
        if (player.Position.y < pos.y && IsVisible(player.Position.x + 0.5, player.Position.y + 0.5, pos.x + 0.5, pos.y)) return true;
        if (player.Position.y > pos.y && IsVisible(player.Position.x + 0.5, player.Position.y + 0.5, pos.x + 0.5, pos.y + 1)) return true;

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

    #region State

    // TODO: Move to separate class?

    private PlayerState GetPlayerState(Player player)
    {
        var width = Parameters.PlayerVisibilityRange * 2 + 1;
        var height = Parameters.PlayerVisibilityRange * 2 + 1;

        var surroundings = new int[height * width];

        var top = player.Position.y - Parameters.PlayerVisibilityRange;
        var left = player.Position.x - Parameters.PlayerVisibilityRange;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surroundings[y * width + x] = ToCellState(player, (top + y, left + x));
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

        if (pos.Equals(player.Position))
        {
            return PLAYER;
        }

        if (IsVisible(player, pos))
        {
            if (pos.Equals(player1.Position) || (player2 != null && pos.Equals(player2.Position)))
            {
                return PLAYER;
            }

            if ((enemy1 != null && pos.Equals(enemy1.Position)) || (enemy2 != null && pos.Equals(enemy2.Position)))
            {
                return ENEMY;
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

    private int ToInventoryState(Inventory inventory) => inventory switch
    {
        Inventory.None => 0,
        Inventory.KeyRed => 1,
        Inventory.KeyGreen => 2,
        Inventory.KeyBlue => 3,
        _ => throw new NotImplementedException(),
    };

    #endregion

    private static Inventory ToInventory(Cell cell) => cell switch
    {
        Cell.KeyRed => Inventory.KeyRed,
        Cell.KeyGreen => Inventory.KeyGreen,
        Cell.KeyBlue => Inventory.KeyBlue,
        _ => Inventory.None,
    };

    private static Cell ToOpenDoor(Cell closedDoor) => closedDoor switch
    {
        Cell.DoorRedClosed => Cell.DoorRedOpen,
        Cell.DoorGreenClosed => Cell.DoorGreenOpen,
        Cell.DoorBlueClosed => Cell.DoorBlueOpen,
        Cell.DoorBlackClosed => Cell.DoorBlackOpen,
        _ => throw new NotImplementedException("Not a closed door"),
    };
}
