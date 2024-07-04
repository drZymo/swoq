using Swoq.Infra;
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
            enemies = enemies.Add(new GameEnemy(GameCharacterId.Enemy1, map.InitialEnemy1Position.Value, Inventory: map.InitialEnemy1Inventory));
        }
        if (map.InitialEnemy2Position.HasValue)
        {
            enemies = enemies.Add(new GameEnemy(GameCharacterId.Enemy2, map.InitialEnemy2Position.Value, Inventory: map.InitialEnemy2Inventory));
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

    private void Move(ref GamePlayer player, Direction direction)
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

    private void Use(ref GamePlayer player, Direction direction)
    {
        var usePos = GetDirectionPosition(player, direction);

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
                    // Cannot use on this
                    throw new UseNotAllowedException(CreateState());

                case Cell.Empty:
                    if (player.Inventory != Inventory.Boulders) throw new InventoryEmptyException(CreateState());
                    PlaceBoulder(ref player, usePos, Cell.Boulders);
                    break;
                case Cell.PressurePlate:
                    if (player.Inventory != Inventory.Boulders) throw new InventoryEmptyException(CreateState());
                    PlaceBoulder(ref player, usePos, Cell.PressurePlateWithBoulders);
                    OpenAllDoors(Cell.DoorBlackClosed);
                    break;

                case Cell.Boulders:
                    if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());
                    PickupInventory(ref player, usePos, Cell.Empty);
                    break;
                case Cell.PressurePlateWithBoulders:
                    if (player.Inventory != Inventory.None) throw new InventoryFullException(CreateState());
                    PickupInventory(ref player, usePos, Cell.PressurePlate);
                    CloseAllDoors(Cell.DoorBlackOpen);
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

    private Position GetDirectionPosition(GamePlayer player, Direction direction) => direction switch
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

    private void EnterCell(ref GamePlayer player, Position position)
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
        if (player.Inventory != Inventory.Boulders) throw new UseNotAllowedException(CreateState());

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

        // Are there players adjacent to the enemy?
        var currentAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (player1 != null && AraAdjacent(enemy, player1)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player1);
        if (player2 != null && AraAdjacent(enemy, player2)) currentAdjacentPlayers = currentAdjacentPlayers.Add(GameCharacterId.Player2);
        var previousAdjacentPlayers = ImmutableHashSet<GameCharacterId>.Empty;
        if (prevPlayer1 != null && AraAdjacent(enemy, prevPlayer1)) previousAdjacentPlayers = previousAdjacentPlayers.Add(GameCharacterId.Player1);
        if (prevPlayer2 != null && AraAdjacent(enemy, prevPlayer2)) previousAdjacentPlayers = previousAdjacentPlayers.Add(GameCharacterId.Player2);

        // Is there a player adjacent for more than 1 tick?, then attack one randomly.
        var adjacentPlayers = currentAdjacentPlayers.Intersect(previousAdjacentPlayers);
        if (adjacentPlayers.Count > 0)
        {
            // Attack
            var adjacentPlayer = adjacentPlayers.PickOne();
            if (player1 != null && adjacentPlayer == GameCharacterId.Player1) DealDamage(ref player1, 1);
            if (player2 != null && adjacentPlayer == GameCharacterId.Player2) DealDamage(ref player2, 1);
        }
        else if (currentAdjacentPlayers.Count == 0)
        {
            // No player adjacent, so move towards closest player
            var closestPlayers = FindClosestVisiblePlayers(enemy.Position, Parameters.EnemyVisibilityRange);
            if (closestPlayers.Count > 0)
            {
                // Chase one of the closest players
                var closestPlayer = closestPlayers.PickOne();
                var dy = closestPlayer.Position.y - enemy.Position.y;
                var dx = closestPlayer.Position.x - enemy.Position.x;
                var nextPos = Math.Abs(dy) > Math.Abs(dx)
                    ? (enemy.Position.y + Math.Sign(dy), enemy.Position.x)
                    : (enemy.Position.y, enemy.Position.x + Math.Sign(dx));
                if (CanMoveTo(nextPos))
                {
                    // Once in a while do not move
                    if (Rnd.Next(0, 100) < 90)
                    {
                        enemy = enemy with { Position = nextPos };
                    }
                }
            }
        }
    }

    private ImmutableList<GamePlayer> FindClosestVisiblePlayers(Position fromPos, int visibilityRange)
    {
        double minDist = double.PositiveInfinity;
        ImmutableList<GamePlayer> closestPlayers = [];

        GamePlayer?[] players = [player1, player2];
        foreach (var player in players)
        {
            if (player == null) continue;

            // Compute distance and check if it is visible from the given position
            var dist = player.Position.DistanceTo(fromPos);
            if (dist > visibilityRange ||
                !IsVisible(fromPos, player.Position))
            {
                continue;
            }

            // Is it closest or equally close?
            if (dist < minDist)
            {
                minDist = dist;
                closestPlayers = [player];
            }
            else if (dist == minDist)
            {
                closestPlayers = closestPlayers.Add(player);
            }
        }

        return closestPlayers;
    }

    private void DealDamage<T>(ref T character, int damage) where T : GameCharacter
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
            // Drop loot only on empty cell
            // This is to prevent overwriting a pressure plate
            // Otherwise loot is simply lost
            var dropPos = character.Position;
            if (map[dropPos] != Cell.Empty)
            {
                dropPos = FindEmptyPosAround(dropPos);
            }
            if (dropPos.IsValid())
            {
                var loot = character.Inventory.ToDroppedLoot();
                map = map.Set(dropPos, loot);
            }

            // Remove loot from inventory
            character = character with { Inventory = Inventory.None };
            // Remove character from game
            character = character with { Position = PositionEx.Invalid };
            lastChangeTick = ticks;
        }

        return character;
    }


    private Position FindEmptyPosAround(Position pos)
    {
        bool IsNotOccupied(Position p) =>
            map[p] == Cell.Empty &&
            player1?.Position != p &&
            player2?.Position != p &&
            !enemies.Any(e => e.Position == p);

        ImmutableList<Position> choices = [];
        void AddIfNotOccupied(Position p)
        {
            if (IsNotOccupied(p)) choices = choices.Add(p);
        }

        AddIfNotOccupied((pos.y - 1, pos.x - 1));
        AddIfNotOccupied((pos.y - 1, pos.x));
        AddIfNotOccupied((pos.y - 1, pos.x + 1));
        AddIfNotOccupied((pos.y, pos.x - 1));
        AddIfNotOccupied((pos.y, pos.x + 1));
        AddIfNotOccupied((pos.y + 1, pos.x - 1));
        AddIfNotOccupied((pos.y + 1, pos.x));
        AddIfNotOccupied((pos.y + 1, pos.x + 1));
        return choices.Count > 0 ? choices.PickOne() : PositionEx.Invalid;
    }

    #region State

    // TODO: Move to separate class?

    private PlayerState GetPlayerState(GamePlayer player)
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

    private int ToCellState(GamePlayer player, Position pos)
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
        const int BOULDERS = 16;

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
                case Cell.Boulders: return BOULDERS;
                case Cell.PressurePlateWithBoulders: return BOULDERS;

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
        Inventory.Boulders => 4,
        _ => throw new NotImplementedException(),
    };

    #endregion
}
