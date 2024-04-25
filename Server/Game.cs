using Swoq.Infra;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    private readonly Map map;

    private abstract record Character(Position Position, Inventory Inventory, int Health);
    private record Player(int Index, Position Position, Inventory Inventory = Inventory.None, int Health = 5, bool HasSword = false) : Character(Position, Inventory, Health);
    private record Enemy(int Index, Position Position, Inventory Inventory = Inventory.None, int Health = 4) : Character(Position, Inventory, Health);

    private Player? player1 = null;
    private Player? player2 = null;
    private IImmutableList<Enemy> enemies = ImmutableList<Enemy>.Empty;

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
            enemies = enemies.Add(new Enemy(1, map.InitialEnemy1Position.Value, Inventory: map.InitialEnemy1Inventory));
        }
        if (map.InitialEnemy2Position.HasValue)
        {
            enemies = enemies.Add(new Enemy(2, map.InitialEnemy2Position.Value, Inventory: map.InitialEnemy2Inventory));
        }
    }

    public Guid Id { get; } = Guid.NewGuid();
    public int Width => map.Width;
    public int Height => map.Height;

    public GameState GetState()
    {
        PlayerState? player1State = player1 != null ? GetPlayerState(player1) : null;
        PlayerState? player2State = player2 != null ? GetPlayerState(player2) : null;
        return new GameState(isFinished, player1State, player2State);
    }

    public void Act(DirectedAction? action1 = null, DirectedAction? action2 = null)
    {
        if (isFinished) throw new GameFinishedException();

        // Pre conditions
        if (action1 != null)
        {
            if (player1 == null) throw new Player1NotPresentException();
            if (!player1.Position.IsValid()) throw new Player1NotPresentException();
            if (player1.Health <= 0) throw new Player1DiedException();
        }
        if (action2 != null)
        {
            if (player2 == null) throw new Player2NotPresentException();
            if (!player2.Position.IsValid()) throw new Player2NotPresentException();
            if (player2.Health <= 0) throw new Player2DiedException();
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
                    throw new UnknownActionException();
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
                    throw new UnknownActionException();
            }
        }

        foreach (var enemy in enemies)
        {
            HandleEnemy(enemy);
        }

        // Check fnished
        if ((player1 == null || !player1.Position.IsValid()) &&
            (player2 == null || !player2.Position.IsValid()))
        {
            isFinished = true;
        }
    }

    private void Move(ref Player player, Direction direction)
    {
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
        var usePos = GetDirectionPosition(player, direction);

        if (TryUseOnEnemy(player, usePos))
        {
            // It was an enemy, do nothing more.
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

    private void HandleEnemy(Enemy enemy)
    {
        if (!enemy.Position.IsValid()) return;
        if (enemy.Health <= 0) return;

        // Find all players that are adjacent to this enemy
        ImmutableHashSet<int> attackables = [];
        if (TryGetPlayerIndexAtPosition((enemy.Position.y - 1, enemy.Position.x), out var playerNorth)) attackables = attackables.Add(playerNorth);
        if (TryGetPlayerIndexAtPosition((enemy.Position.y + 1, enemy.Position.x), out var playerSouth)) attackables = attackables.Add(playerSouth);
        if (TryGetPlayerIndexAtPosition((enemy.Position.y, enemy.Position.x - 1), out var playerWest)) attackables = attackables.Add(playerWest);
        if (TryGetPlayerIndexAtPosition((enemy.Position.y, enemy.Position.x + 1), out var playerEast)) attackables = attackables.Add(playerEast);

        if (!attackables.IsEmpty)
        {
            // Attack one randomly
            var attackable = attackables.PickOne();
            if (attackable == 1 && player1 != null) DealDamage(ref player1, 1);
            if (attackable == 2 && player2 != null) DealDamage(ref player2, 1);
        }
    }

    private static Position GetDirectionPosition(Player player, Direction direction) => direction switch
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

            default:
                throw new NotImplementedException(); // Should not be possible
        }
    }

    private void PickupKey(ref Player player, Position position)
    {
        // Cannot pickup if inventory is full
        if (player.Inventory != Inventory.None) throw new InventoryFullException();

        // Is it an item that can be picked up?
        var item = map[position].ToInventory();
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

    private bool TryUseOnEnemy(Player player, Position usePos)
    {
        foreach (var enemy in enemies)
        {
            if (usePos.Equals(enemy.Position))
            {
                if (!player.HasSword) throw new NoSwordException();

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
        if (player.Inventory != item) throw new UseNotAllowedException();

        // Oopen all the doors of the same type
        var closedDoor = map[usePosition];
        OpenAllDoors(closedDoor);

        // Remove key from inventory
        player = player with { Inventory = Inventory.None };
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
                    map[y, x] = openDoor;
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
                if (map[y, x] == openDoor)
                {
                    map[y, x] = closedDoor;
                }
            }
        }
    }

    private void DealDamage<T>(ref T character, int damage) where T : Character
    {
        character = character with { Health = character.Health - damage };
        if (character.Health <= 0)
        {
            // Drop loot
            map[character.Position] = character.Inventory.ToDroppedLoot();
            character = character with { Inventory = Inventory.None };
            // Remove from game
            character = character with { Position = PositionEx.Invalid };
        }
    }

    private bool TryGetPlayerIndexAtPosition(Position position, out int index)
    {
        if (player1 != null && position.Equals(player1.Position))
        {
            index = 1;
            return true;
        }

        if (player2 != null && position.Equals(player2.Position))
        {
            index = 2;
            return true;
        }

        index = default;
        return false;
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

        if (pos.Equals(player.Position))
        {
            return PLAYER;
        }

        if (IsVisible(player, pos))
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
