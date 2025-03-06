using Swoq.Interface;
using System.Text;
using PosIndex = int;

internal class ActionPlanner(int mapHeight, int mapWidth, int visibilityRange)
{
    private readonly Tile[] map = new Tile[mapHeight * mapWidth];
    private PosIndex playerPos = -1;

    private int level = -1;
    private readonly int[] distances = new int[mapHeight * mapWidth];
    private readonly PosIndex[] paths = new PosIndex[mapHeight * mapWidth];

    private Inventory inventory = Inventory.None;

    private readonly Dictionary<Tile, HashSet<PosIndex>> tilePositions = [];

    public DirectedAction GetNextAction(State state)
    {
        UpdateState(mapHeight, mapWidth, state);
        //Console.WriteLine($"Inventory: {inventory}");

        var action = DirectedAction.None;

        if (action == DirectedAction.None) action = TryMoveToExit();
        if (action == DirectedAction.None) action = TryGetKey();
        if (action == DirectedAction.None) action = TryOpenDoor();
        if (action == DirectedAction.None) action = TryDropBoulder();
        if (action == DirectedAction.None) action = TryExplore();
        if (action == DirectedAction.None) action = TryPickupBoulder();

        return action;
    }

    private DirectedAction TryMoveToExit()
    {
        if (inventory == Inventory.Boulder) return DirectedAction.None;

        return tilePositions.TryGetValue(Tile.Exit, out var exitPositions)
            ? MoveToClosest(exitPositions)
            : DirectedAction.None;
    }

    private DirectedAction TryGetKey()
    {
        if (inventory != Inventory.None) return DirectedAction.None;

        var grabPositions = new List<PosIndex>();

        if (HasTiles(Tile.KeyRed) && HasTiles(Tile.DoorRed))
        {
            grabPositions.AddRange(tilePositions[Tile.KeyRed]);
        }
        if (HasTiles(Tile.KeyGreen) && HasTiles(Tile.DoorGreen))
        {
            grabPositions.AddRange(tilePositions[Tile.KeyGreen]);
        }
        if (HasTiles(Tile.KeyBlue) && HasTiles(Tile.DoorBlue))
        {
            grabPositions.AddRange(tilePositions[Tile.KeyBlue]);
        }

        return grabPositions.Count > 0 ? MoveToClosest(grabPositions) : DirectedAction.None;
    }

    private DirectedAction TryOpenDoor()
    {
        if (inventory == Inventory.None) return DirectedAction.None;

        if (inventory == Inventory.KeyRed && HasTiles(Tile.DoorRed))
        {
            return UseClosest(tilePositions[Tile.DoorRed]);
        }
        if (inventory == Inventory.KeyGreen && HasTiles(Tile.DoorGreen))
        {
            return UseClosest(tilePositions[Tile.DoorGreen]);
        }
        if (inventory == Inventory.KeyBlue && HasTiles(Tile.DoorBlue))
        {
            return UseClosest(tilePositions[Tile.DoorBlue]);
        }

        return DirectedAction.None;
    }

    private DirectedAction TryPickupBoulder()
    {
        if (inventory != Inventory.None) return DirectedAction.None;

        if (HasTiles(Tile.Boulder))
        {
            return UseClosest(tilePositions[Tile.Boulder]);
        }

        return DirectedAction.None;
    }

    private DirectedAction TryDropBoulder()
    {
        if (inventory != Inventory.Boulder) return DirectedAction.None;

        // Only drop when exit is reachable
        var hasExit = HasTiles(Tile.Exit);
        if (!hasExit) return DirectedAction.None;
        var exitReachable = MoveToClosest(tilePositions[Tile.Exit]) != DirectedAction.None;
        if (!exitReachable) return DirectedAction.None;

        // Find empty spot surrounded by other empty
        var emptyPositions = tilePositions[Tile.Empty];

        var targets = new List<PosIndex>();
        foreach (var pos in emptyPositions)
        {
            // TODO: Check for bounds
            if (emptyPositions.Contains(pos - mapWidth - 1)
                && emptyPositions.Contains(pos - mapWidth)
                && emptyPositions.Contains(pos - mapWidth + 1)
                && emptyPositions.Contains(pos - 1)
                && emptyPositions.Contains(pos + 1)
                && emptyPositions.Contains(pos + mapWidth - 1)
                && emptyPositions.Contains(pos + mapWidth)
                && emptyPositions.Contains(pos + mapWidth + 1))
            {
                targets.Add(pos);
            }
        }

        return targets.Count > 0 ? UseClosest(targets) : DirectedAction.None;
    }

    private DirectedAction TryExplore()
    {
        var closestUnknown = GetClosestUnknown();
        return closestUnknown.HasValue ? MoveTo(closestUnknown.Value) : DirectedAction.None;
    }

    private DirectedAction MoveTo(PosIndex position)
    {
        var route = new List<PosIndex>();
        var routePos = position;
        route.Add(routePos);
        while (routePos != playerPos)
        {
            routePos = paths[routePos];
            route.Add(routePos);
        }

        var adjacentPos = route[^2];

        return GetMoveDirection(adjacentPos);
    }

    private DirectedAction MoveToClosest(IEnumerable<PosIndex> positions)
    {
        var closestPos = -1;
        var closestDist = int.MaxValue;

        foreach (var pos in positions)
        {
            var dist = distances[pos];
            if (dist < closestDist)
            {
                closestDist = dist;
                closestPos = pos;
            }
        }

        return MoveTo(closestPos);
    }

    private DirectedAction UseClosest(IEnumerable<PosIndex> positions)
    {
        // Standing next to any of the positions?
        foreach (var pos in positions)
        {
            if (AreAdjacent(pos, playerPos))
            {
                return GetUseDirection(pos);
            }
        }

        // Find closest empy position adjacent to any of the positions
        var closestPos = -1;
        var closestDist = int.MaxValue;

        foreach (var pos in AdjacentPositions(positions))
        {
            var dist = distances[pos];
            if (dist < closestDist)
            {
                closestDist = dist;
                closestPos = pos;
            }
        }

        return MoveTo(closestPos);
    }

    private DirectedAction GetMoveDirection(PosIndex pos)
    {
        var delta = pos - playerPos;
        if (delta == 1) return DirectedAction.MoveEast;
        if (delta == -1) return DirectedAction.MoveWest;
        if (delta == mapWidth) return DirectedAction.MoveSouth;
        if (delta == -mapWidth) return DirectedAction.MoveNorth;
        return DirectedAction.None;
    }

    private DirectedAction GetUseDirection(PosIndex pos)
    {
        var delta = pos - playerPos;
        if (delta == 1) return DirectedAction.UseEast;
        if (delta == -1) return DirectedAction.UseWest;
        if (delta == mapWidth) return DirectedAction.UseSouth;
        if (delta == -mapWidth) return DirectedAction.UseNorth;
        return DirectedAction.None;
    }

    private PosIndex? GetClosestUnknown()
    {
        var closestPos = -1;
        var closestDist = int.MaxValue;

        for (var y = 1; y < mapHeight - 1; y++)
        {
            for (var x = 1; x < mapWidth - 1; x++)
            {
                var posIndex = y * mapWidth + x;
                if (map[posIndex] != Tile.Empty) continue;

                if (map[posIndex - mapWidth] == Tile.Unknown ||
                    map[posIndex + mapWidth] == Tile.Unknown ||
                    map[posIndex - 1] == Tile.Unknown ||
                    map[posIndex + 1] == Tile.Unknown)
                {
                    var dist = distances[posIndex];
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestPos = posIndex;
                    }
                }
            }
        }

        return closestPos >= 0 ? closestPos : null;
    }

    private void UpdateState(int mapHeight, int mapWidth, State state)
    {
        playerPos = state.PlayerState.Position.Y * mapWidth + state.PlayerState.Position.X;
        if (state.Level != level)
        {
            Array.Fill(map, Tile.Unknown);
            tilePositions.Clear();
        }
        level = state.Level;
        inventory = state.PlayerState.Inventory;

        CopyPlayerStateToMap(state);

        ComputePaths();
    }

    private void CopyPlayerStateToMap(State state)
    {
        if (state.PlayerState.Surroundings.Count == 0) return;
        var playerPosY = playerPos / mapWidth;
        var playerPosX = playerPos % mapWidth;

        var surroundingsIndex = 0;
        for (var dy = -visibilityRange; dy <= visibilityRange; dy++)
        {
            for (var dx = -visibilityRange; dx <= visibilityRange; dx++)
            {
                var tile = state.PlayerState.Surroundings[surroundingsIndex++];

                if (tile == Tile.Unknown) continue;

                // Hide player
                if (tile == Tile.Player) tile = Tile.Empty;

                var y = playerPosY + dy;
                var x = playerPosX + dx;
                if (y < 0 || y >= mapHeight || x < 0 || x >= mapWidth) continue;

                var posIndex = y * mapWidth + x;

                var oldTile = map[posIndex];
                map[posIndex] = tile;

                if (oldTile != tile)
                {
                    if (tilePositions.TryGetValue(oldTile, out var oldPosition))
                    {
                        oldPosition.Remove(posIndex);
                        if (oldPosition.Count == 0)
                        {
                            tilePositions.Remove(oldTile);
                        }
                    }
                    if (tilePositions.TryGetValue(tile, out var newPosition))
                    {
                        newPosition.Add(posIndex);
                    }
                    else
                    {
                        tilePositions[tile] = [posIndex];
                    }
                }
            }
        }

        if (playerPos >= 0)
        {
            map[playerPos] = Tile.Player;
        }
    }

    private void ComputePaths()
    {
        var todo = new Queue<PosIndex>();
        Array.Fill(distances, int.MaxValue);
        Array.Fill(paths, -1);

        todo.Enqueue(playerPos);
        distances[playerPos] = 0;

        void Enqueue(PosIndex curPos, int curDist, PosIndex nextPos)
        {
            if (IsWall(map[nextPos])) return;

            var nextDist = distances[nextPos];
            if (curDist + 1 < nextDist)
            {
                distances[nextPos] = curDist + 1;
                paths[nextPos] = curPos;
                todo.Enqueue(nextPos);
            }
        }

        while (todo.Count > 0)
        {
            var curPos = todo.Dequeue();
            var curDist = distances[curPos];

            var y = curPos / mapWidth;
            var x = curPos % mapWidth;
            if (y > 0) Enqueue(curPos, curDist, curPos - mapWidth);
            if (y < mapHeight - 1) Enqueue(curPos, curDist, curPos + mapWidth);
            if (x > 0) Enqueue(curPos, curDist, curPos - 1);
            if (x < mapWidth - 1) Enqueue(curPos, curDist, curPos + 1);
        }
    }

    public void PrintMap()
    {
        var posIndex = 0;
        var row = new StringBuilder();
        for (var y = 0; y < mapHeight; y++)
        {
            for (var x = 0; x < mapWidth; x++, posIndex++)
            {
                var tile = map[posIndex];
                row.Append(tile switch
                {
                    Tile.Unknown => $"{ConsoleEx.GRAY}.·{ConsoleEx.DEFAULT}",
                    Tile.Empty => "  ",
                    Tile.Wall => $"██",
                    Tile.Player => $"{ConsoleEx.CYAN}{{}}{ConsoleEx.DEFAULT}",
                    Tile.Exit => $"{ConsoleEx.MAGENTA}><{ConsoleEx.DEFAULT}",
                    Tile.DoorRed => $"{ConsoleEx.RED}##{ConsoleEx.DEFAULT}",
                    Tile.DoorGreen => $"{ConsoleEx.GREEN}##{ConsoleEx.DEFAULT}",
                    Tile.DoorBlue => $"{ConsoleEx.BLUE}##{ConsoleEx.DEFAULT}",
                    Tile.KeyRed => $"{ConsoleEx.RED}=${ConsoleEx.DEFAULT}",
                    Tile.KeyGreen => $"{ConsoleEx.GREEN}=${ConsoleEx.DEFAULT}",
                    Tile.KeyBlue => $"{ConsoleEx.BLUE}=${ConsoleEx.DEFAULT}",
                    Tile.Boulder => $"{ConsoleEx.YELLOW}@@{ConsoleEx.DEFAULT}",
                    _ => "??"
                });
            }
            Console.WriteLine(row.ToString());
            row.Clear();
        }
    }

    private static bool IsWall(Tile tile) => tile switch
    {
        Tile.Unknown => true,
        Tile.Wall => true,
        Tile.DoorRed => true,
        Tile.DoorGreen => true,
        Tile.DoorBlue => true,
        Tile.Boulder => true,
        _ => false,
    };

    private bool AreAdjacent(PosIndex a, PosIndex b)
    {
        var ay = a / mapWidth;
        var ax = a % mapWidth;
        var by = b / mapWidth;
        var bx = b % mapWidth;

        var dy = Math.Abs(ay - by);
        var dx = Math.Abs(ax - bx);
        return (dy == 1 && dx == 0) || (dy == 0 && dx == 1);
    }

    IEnumerable<PosIndex> AdjacentPositions(IEnumerable<PosIndex> positions)
    {
        var emptyTiles = tilePositions[Tile.Empty];
        foreach (var pos in positions)
        {
            var y = pos / mapWidth;
            var x = pos % mapWidth;
            if (x > 0)
            {
                var west = pos - 1;
                if (emptyTiles.Contains(west)) yield return west;
            }
            if (x < mapWidth - 1)
            {
                var east = pos + 1;
                if (emptyTiles.Contains(east)) yield return east;
            }
            if (y > 0)
            {
                var north = pos - mapWidth;
                if (emptyTiles.Contains(north)) yield return north;
            }
            if (y < mapHeight - 1)
            {
                var south = pos + mapWidth;
                if (emptyTiles.Contains(south)) yield return south;
            }
        }
    }

    private bool HasTiles(Tile tile) => tilePositions.TryGetValue(tile, out var p) && p.Count > 0;
}
