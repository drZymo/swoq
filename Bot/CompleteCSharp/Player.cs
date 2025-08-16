using Swoq.Interface;
using PosIndex = int;

internal struct Player(int mapHeight, int mapWidth, Dictionary<Tile, HashSet<PosIndex>> tilePositions)
{
    public PosIndex pos = -1;
    public int x = -1;
    public int y = -1;
    public Inventory inventory = Inventory.None;
    public int health = -1;
    public bool hasSword = false;

    public readonly int[] distances = new int[mapHeight * mapWidth];
    public readonly PosIndex[] paths = new PosIndex[mapHeight * mapWidth];

    public DirectedAction action = DirectedAction.None;
    public DirectedAction nextAction = DirectedAction.None;

    public PosIndex targetPlatePosition = -1;
    public int remainOnPlateCounter = -1;
    private Inventory remainOnPlateInventory = Inventory.None;
    private int remainOnPlateHealth = -1;
    private bool remainOnPlateHasSword = false;

    public PosIndex targetPosition = -1;

    public void SyncFromState(PlayerState? state)
    {
        x = state?.Position.X ?? -1;
        y = state?.Position.Y ?? -1;
        pos = y * mapWidth + x;
        inventory = state?.Inventory ?? Inventory.None;
        health = state?.Health ?? -1;
        hasSword = state?.HasSword ?? false;
    }

    public readonly bool IsPresent => pos >= 0;

    public readonly bool CanAct => action == DirectedAction.None && remainOnPlateCounter <= 0;

    public readonly bool CanMoveNorth => y > 0 &&
      tilePositions.TryGetValue(Tile.Empty, out var emptyPositions) &&
      emptyPositions.Contains(pos - mapWidth);

    public readonly bool CanMoveSouth => y < mapHeight - 1 &&
            tilePositions.TryGetValue(Tile.Empty, out var emptyPositions) &&
            emptyPositions.Contains(pos + mapWidth);

    public readonly bool CanMoveEast => x < mapWidth - 1 &&
            tilePositions.TryGetValue(Tile.Empty, out var emptyPositions) &&
            emptyPositions.Contains(pos + 1);

    public readonly bool CanMoveWest => x > 0 &&
            tilePositions.TryGetValue(Tile.Empty, out var emptyPositions) &&
            emptyPositions.Contains(pos - 1);

    public readonly bool CanReach(Tile tile)
    {
        var paths = this.paths;
        return tilePositions.TryGetValue(tile, out var positions) && positions.Any(p => paths[p] >= 0);
    }

    public readonly bool CanReach(PosIndex pos)
    {
        return paths[pos] >= 0;
    }

    public void Update(ref Player other)
    {
        if (targetPlatePosition >= 0 && pos == targetPlatePosition)
        {
            if (remainOnPlateCounter < 0)
            {
                remainOnPlateCounter = 100;
                remainOnPlateInventory = other.inventory;
                remainOnPlateHealth = other.health;
                remainOnPlateHasSword = other.hasSword;
            }

            if (other.inventory != remainOnPlateInventory ||
                other.health != remainOnPlateHealth ||
                other.hasSword != remainOnPlateHasSword)
            {
                targetPlatePosition = -1;
                remainOnPlateCounter = 20; // Wait a few ticks after release before really moving
            }
        }

        if (remainOnPlateCounter >= 0)
        {
            remainOnPlateCounter--;

            if (remainOnPlateCounter < 0)
            {
                targetPlatePosition = -1;
            }
        }

        if (targetPosition >= 0 && pos == targetPosition)
        {
            targetPosition = -1;
        }
    }

    public readonly PosIndex NextPosition => action switch
    {
        DirectedAction.MoveNorth => pos - mapWidth,
        DirectedAction.MoveEast => pos + 1,
        DirectedAction.MoveSouth => pos + mapWidth,
        DirectedAction.MoveWest => pos - 1,
        _ => pos
    };

    public readonly bool CanReachExit => CanReach(Tile.Exit);

    public void Reset()
    {
        SyncFromState(null);

        action = DirectedAction.None;
        nextAction = DirectedAction.None;

        targetPlatePosition = -1;
        remainOnPlateCounter = -1;
        remainOnPlateInventory = Inventory.None;
        remainOnPlateHealth = -1;
        remainOnPlateHasSword = false;

        targetPosition = -1;
    }
}
