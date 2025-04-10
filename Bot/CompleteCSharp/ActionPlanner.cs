using Swoq.Interface;
using System.Diagnostics;
using System.Text;
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
    public bool remainOnPlate = false;
    public int remainOnPlateCounter = 0;
    private Inventory remainOnPlateInventory = Inventory.None;
    private int remainOnPlateHealth = -1;
    private bool remainOnPlateHasSword = false;

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

    public readonly bool CanAct => action == DirectedAction.None && !remainOnPlate && remainOnPlateCounter <= 0;

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

    public void UpdateRemainOnPlate(ref Player other)
    {
        if (targetPlatePosition >= 0 && pos == targetPlatePosition)
        {
            remainOnPlate = true;
            remainOnPlateInventory = other.inventory;
            remainOnPlateHealth = other.health;
            remainOnPlateHasSword = other.hasSword;
        }

        if (remainOnPlate)
        {
            if (other.inventory != remainOnPlateInventory ||
                other.health != remainOnPlateHealth ||
                other.hasSword != remainOnPlateHasSword)
            {
                remainOnPlate = false;
                remainOnPlateCounter = 20; // Wait a few ticks after release before really moving
            }
        }
        else if (remainOnPlateCounter > 0)
        {
            remainOnPlateCounter--;
        }

        targetPlatePosition = -1;
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
        remainOnPlate = false;
        remainOnPlateCounter = 0;
        remainOnPlateInventory = Inventory.None;
        remainOnPlateHealth = -1;
        remainOnPlateHasSword = false;
    }
}

internal class ActionPlanner
{
    private readonly int mapHeight;
    private readonly int mapWidth;
    private readonly int visibilityRange;

    private readonly Tile[] map;
    private Player player1;
    private Player player2;
    private int tick = 0;
    private int level = -1;
    private readonly Dictionary<Tile, HashSet<PosIndex>> tilePositions = [];

    private readonly HashSet<PosIndex> plateDoorPositions = [];
    private readonly List<PosIndex> keyPickupPositions = [];
    private readonly Dictionary<PosIndex, int> platesWithBoulders = [];

    private int level21State = 0;
    private int level22State = 0;
    private PosIndex bossPos = -1;

    public ActionPlanner(int mapHeight, int mapWidth, int visibilityRange)
    {
        this.mapHeight = mapHeight;
        this.mapWidth = mapWidth;
        this.visibilityRange = visibilityRange;

        map = new Tile[mapHeight * mapWidth];
        player1 = new(mapHeight, mapWidth, tilePositions);
        player2 = new(mapHeight, mapWidth, tilePositions);
    }

    public (DirectedAction action1, DirectedAction action) GetNextAction(State state)
    {
        UpdateState(state);

        UpdateRemainOnPlate();
        StorePlateDoorPositions();

        // Gather some information
        var canBothReachExit = (!player1.IsPresent || player1.CanReachExit) && (!player2.IsPresent || player2.CanReachExit);

        // Find plate positions
        var platePositions = new HashSet<PosIndex>();
        if (tilePositions.TryGetValue(Tile.PressurePlateRed, out var pr)) platePositions.UnionWith(pr);
        if (tilePositions.TryGetValue(Tile.PressurePlateGreen, out var pg)) platePositions.UnionWith(pg);
        if (tilePositions.TryGetValue(Tile.PressurePlateBlue, out var pb)) platePositions.UnionWith(pb);

        player1.action = player1.nextAction;
        player1.nextAction = DirectedAction.None;
        player2.action = player2.nextAction;
        player2.nextAction = DirectedAction.None;

        Debug.WriteLine($"-- player #1--");
        HandlePlayer(ref player1, ref player2, canBothReachExit, platePositions);

        // Update map accordingly
        if (player1.pos >= 0)
        {
            SetTile(player1.pos, Tile.Empty);
        }
        player1.pos = player1.NextPosition;
        if (player1.pos >= 0 && map[player1.pos] != Tile.Exit)
        {
            SetTile(player1.pos, Tile.Player);
        }

        // Recompute paths after movement of player 1
        ComputePaths(ref player2);

        Debug.WriteLine($"-- player #2 --");
        HandlePlayer(ref player2, ref player1, canBothReachExit, platePositions);

        return (player1.action, player2.action);
    }

    private void HandlePlayer(ref Player player, ref Player other, bool canBothReachExit, HashSet<int> platePositions)
    {
        var prevAction = player.action;

        void Print(string step, ref Player player)
        {
            if (player.action != prevAction)
            {
                Debug.WriteLine($"{step} {player.action}");
                prevAction = player.action;
            }
        }

        if (level == 22)
        {
            HandleLevel22(ref player, ref other, canBothReachExit, platePositions);
            Print("lvl22", ref player);
            return;
        }
        if (level == 21)
        {
            HandleLevel21(ref player, ref other, canBothReachExit, platePositions);
            Print("lvl21", ref player);
            return;
        }

        HandleLevel20(ref player, ref other, platePositions);
        Print("lvl20", ref player);

        MoveToExit(ref player, canBothReachExit);
        Print("move to exit", ref player);

        PickupHealth(ref player, ref other);
        Print("pickup health", ref player);

        if (!player.hasSword) Pickup(Tile.Sword, ref player);
        Print("pickup sword", ref player);

        GetKey(ref player);
        Print("pickup key", ref player);

        OpenDoor(ref player);
        Print("open door", ref player);

        Attack(ref player);
        Print("attack", ref player);

        Explore(ref player);
        Print("explore", ref player);

        PlaceBoulderOnPlate(ref player, platePositions);
        Print("boulder on plate", ref player);

        // Only player 1 will stand on plates
        if (player.pos == player1.pos)
        {
            MoveToPlate(ref player, platePositions);
            Print("move to plate", ref player);
        }

        WaitAtPressurePlateDoor(ref player);
        Print("wait at door", ref player);

        PickupBoulder(ref player);
        Print("pickup boulder", ref player);

        WalkRandom(ref player);
        Print("random", ref player);
    }

    private void UpdateRemainOnPlate()
    {
        foreach (var pos in platesWithBoulders.Keys)
        {
            var count = platesWithBoulders[pos];
            if (count > 1)
            {
                platesWithBoulders[pos] = count - 1;
            }
            else
            {
                platesWithBoulders.Remove(pos);
            }
        }

        player1.UpdateRemainOnPlate(ref player2);
        player2.UpdateRemainOnPlate(ref player1);

        // Step off plate when enemy can be crushed
        if (tilePositions.TryGetValue(Tile.Enemy, out var enemyPositions))
        {
            if (enemyPositions.Any(p => plateDoorPositions.Contains(p)))
            {
                player1.remainOnPlate = false;
                player1.remainOnPlateCounter = 0;
                player2.remainOnPlate = false;
                player2.remainOnPlateCounter = 0;
            }
        }
    }

    private void StorePlateDoorPositions()
    {
        if (tilePositions.ContainsKey(Tile.PressurePlateRed) && tilePositions.TryGetValue(Tile.DoorRed, out var dr))
        {
            foreach (var p in dr)
            {
                plateDoorPositions.Add(p);
            }
        }
        if (tilePositions.ContainsKey(Tile.PressurePlateGreen) && tilePositions.TryGetValue(Tile.DoorGreen, out var dg))
        {
            foreach (var p in dg)
            {
                plateDoorPositions.Add(p);
            }
        }
        if (tilePositions.ContainsKey(Tile.PressurePlateBlue) && tilePositions.TryGetValue(Tile.DoorBlue, out var db))
        {
            foreach (var p in db)
            {
                plateDoorPositions.Add(p);
            }
        }
    }

    private void HandleLevel20(ref Player player, ref Player other, HashSet<int> platePositions)
    {
        if (level != 20) return;

        if (other.IsPresent && other.x < 8 && other.y < 11)
        {
            MoveToPlate(ref player, platePositions);
        }
        else
        {
            player.remainOnPlate = false;
            player.remainOnPlateCounter = 0;
            player.targetPlatePosition = -1;
        }
    }

    private void HandleLevel21(ref Player player, ref Player other, bool canBothReachExit, HashSet<int> platePositions)
    {
        if (level != 21) return;

        if (level21State == 0) // Find boulder and plates
        {
            if (platePositions.Count == 2 && tilePositions.ContainsKey(Tile.Boulder))
            {
                level21State = 1;
            }

        }
        if (level21State == 1) // Move player1 to plate and player2 to boulder
        {
            if (player1.remainOnPlate && player2.inventory == Inventory.Boulder)
            {
                level21State = 2;
            }
            else
            {
                if (player.pos == player1.pos)
                {
                    MoveToPlate(ref player, platePositions);
                }
                else if (player.pos == player2.pos)
                {
                    PickupBoulder(ref player);
                }
            }
        }
        if (level21State == 2) // Place boulder on plate
        {
            if (player.pos == player1.pos)
            {
                player.remainOnPlateCounter = 100;
            }

            if (platePositions.Count == 0)
            {
                level21State = 3;
            }
            else
            {
                if (player.pos == player2.pos)
                {
                    PlaceBoulderOnPlate(ref player, platePositions);
                }
            }
        }
        if (level21State == 3) // Pick sword
        {
            if (player.pos == player1.pos)
            {
                player.remainOnPlateCounter = 100;
            }

            if (player2.hasSword && DistanceBetween(player1.pos, player2.pos) < 5)
            {
                level21State = 4;
                player1.remainOnPlate = false;
                player1.remainOnPlateCounter = 0;
                player1.action = MoveToClosest(AdjacentPositions([player2.pos]), ref player1);
            }
            if (player.pos == player2.pos)
            {
                if (player.hasSword)
                {
                    player.action = MoveToClosest(AdjacentPositions([other.pos]), ref player);
                }
                else if (tilePositions.ContainsKey(Tile.Sword))
                {
                    Pickup(Tile.Sword, ref player);
                }
                else
                {
                    WaitAtPressurePlateDoor(ref player);
                }
            }
        }
        if (level21State == 4) // swap places
        {
            if (player2.remainOnPlate)
            {
                level21State = 5;
            }
            else
            {
                if (player.pos == player2.pos)
                {
                    MoveToPlate(ref player, platePositions);
                }
            }
        }
        if (level21State == 5) // player 1 pick sword
        {
            if (player.pos == player2.pos)
            {
                player.remainOnPlateCounter = 100; // stay on plate
            }

            if (player1.hasSword && DistanceBetween(player1.pos, player2.pos) < 5)
            {
                level21State = 6;
                player2.remainOnPlate = false;
                player2.remainOnPlateCounter = 0;
                player2.action = MoveToClosest(AdjacentPositions([player1.pos]), ref player2);
            }
            if (player.pos == player1.pos)
            {
                if (player1.hasSword)
                {
                    player.action = MoveToClosest(AdjacentPositions([other.pos]), ref player);
                }
                else if (tilePositions.ContainsKey(Tile.Sword))
                {
                    Pickup(Tile.Sword, ref player);
                }
                else
                {
                    WaitAtPressurePlateDoor(ref player);
                }
            }
        }
        if (level21State == 6) // pickup health
        {
            PickupHealth(ref player, ref other);
            Explore(ref player);
            if (player.action == DirectedAction.None && !HasTiles(Tile.Health))
            {
                level21State = 7;
            }
        }
        if (level21State == 7) // move closer
        {
            if (DistanceBetween(player.pos, other.pos) > 4)
            {
                player.action = MoveToClosest(AdjacentPositions([other.pos]), ref player);
            }
            else
            {
                level21State = 8;
            }
        }
        if (level21State == 8) // explore and attack
        {
            // Stay close together
            if (HasTiles(Tile.Enemy) && DistanceBetween(player.pos, other.pos) > 3)
            {
                player.action = MoveToClosest(AdjacentPositions([other.pos]), ref player);
            }
            else
            {
                Attack(ref player);
            }
        }

        MoveToExit(ref player, canBothReachExit);
        GetKey(ref player);
        OpenDoor(ref player);
        PickupHealth(ref player, ref other);
        Explore(ref player);

        // randomly disable player 2
        if (Random.Shared.NextDouble() < 0.1) player2.action = DirectedAction.None;
    }

    private void HandleLevel22(ref Player player, ref Player other, bool canBothReachExit, HashSet<int> platePositions)
    {
        if (level != 22) return;

        if (!tilePositions.TryGetValue(Tile.Boss, out var bossPositions))
        {
            bossPositions = [];
        }

        if (bossPos < 0 && bossPositions.Count > 0)
        {
            bossPos = bossPositions.First();
        }

        if (level22State == 0) // move to plate
        {
            if (other.remainOnPlate)
            {
                if (plateDoorPositions.Contains(player.pos))
                {
                    level22State = 1;
                }
                else
                {
                    player.action = MoveToClosest(plateDoorPositions, ref player);
                    if (player.action == DirectedAction.None)
                    {
                        player.action = MoveToClosest(AdjacentPositions(plateDoorPositions), ref player);
                    }
                }
            }
            MoveToPlate(ref player, platePositions);
        }
        if (level22State == 1) // move toward boss
        {
            if (other.remainOnPlate)
            {
                foreach (var bossPos in bossPositions)
                {
                    if (DistanceBetween(player.pos, bossPos) > 5)
                    {
                        player.action = MoveToClosest(AdjacentPositions([bossPos]), ref player);
                    }
                    else
                    {
                        level22State = 2;
                        break;
                    }
                }
            }
        }
        if (level22State == 2) // move to plate with boss
        {
            if (other.remainOnPlate)
            {
                foreach (var bossPos in bossPositions)
                {
                    if (DistanceBetween(player.pos, bossPos) < 4)
                    {
                        player.action = MoveToClosest(AdjacentPositions([other.pos]), ref player);
                    }
                }
            }
            else if (player.remainOnPlate)
            {
                // crush
                foreach (var pos in bossPositions)
                {
                    if (plateDoorPositions.Contains(pos))
                    {
                        player.action = DirectedAction.MoveWest;
                        other.remainOnPlate = false;
                        other.remainOnPlateCounter = 0;
                        level22State = 3;
                    }
                }
            }
        }
        if (level22State == 3) // look at initial boss pos
        {
            if (player.pos != bossPos)
            {
                player.action = MoveTo(bossPos, ref player);
            }
            else
            {
                level22State = 4;
            }
        }
        if (level22State == 4) // pick key for exit
        {
            if (player.inventory == Inventory.None)
            {
                GetKey(ref player);
            }
            else
            {
                level22State = 5;
            }
        }
        if (level22State == 5) // open exit door
        {
            if (player.inventory != Inventory.None)
            {
                OpenDoor(ref player);
            }
            if (canBothReachExit)
            {
                level22State = 6;
            }
        }
        if (level22State == 6) // pick treasure
        {
            if (player.inventory == Inventory.Treasure && other.inventory == Inventory.Treasure)
            {
                level22State = 7;
            }

            if (player.inventory == Inventory.None)
            {
                Pickup(Tile.Treasure, ref player);
            }
        }
        if (level22State == 7) // move to exit
        {
            MoveToExit(ref player, canBothReachExit);
        }
        Explore(ref player);
    }

    private void MoveToExit(ref Player player, bool canBothReachExit)
    {
        if (!player.CanAct) return;

        // Only move to exit when both players can exit
        if (!canBothReachExit) return;

        if (!tilePositions.TryGetValue(Tile.Exit, out var exitPositions)) return;

        // Only exit without boulder
        if (player.inventory == Inventory.Boulder)
        {
            // Find empty spot surrounded by other empty
            var emptyPositions = tilePositions[Tile.Empty];

            var targets = new List<PosIndex>();
            foreach (var pos in emptyPositions)
            {
                if (map[pos - mapWidth - 1] is Tile.Empty or Tile.Player
                    && map[pos - mapWidth] is Tile.Empty or Tile.Player
                    && map[pos - mapWidth + 1] is Tile.Empty or Tile.Player
                    && map[pos - 1] is Tile.Empty or Tile.Player
                    && map[pos + 1] is Tile.Empty or Tile.Player
                    && map[pos + mapWidth - 1] is Tile.Empty or Tile.Player
                    && map[pos + mapWidth] is Tile.Empty or Tile.Player
                    && map[pos + mapWidth + 1] is Tile.Empty or Tile.Player)
                {
                    targets.Add(pos);
                }
            }

            if (targets.Count > 0)
            {
                player.action = UseClosest(targets, ref player, out _);
            }
        }
        else
        {
            player.action = MoveToClosest(exitPositions, ref player);
        }
    }

    private void PickupHealth(ref Player player, ref Player other)
    {
        if (!player.CanAct) return;

        // Only pickup health if able to attack
        if (!player.hasSword) return;

        // Keep health distributed between players
        if (other.IsPresent && (player.health - other.health >= 3)) return;

        Pickup(Tile.Health, ref player);
    }

    private void Pickup(Tile tile, ref Player player)
    {
        if (!player.CanAct) return;

        if (tilePositions.TryGetValue(tile, out var positions))
        {
            player.action = MoveToClosest(positions, ref player);
        }
    }

    private void GetKey(ref Player player)
    {
        if (!player.CanAct || player.inventory != Inventory.None) return;

        if (keyPickupPositions.Count == 0) return;

        player.action = MoveToClosest(keyPickupPositions, ref player);
    }

    private void OpenDoor(ref Player player)
    {
        if (!player.CanAct) return;

        if (player.inventory == Inventory.KeyRed && tilePositions.TryGetValue(Tile.DoorRed, out var redDoors))
        {
            player.action = UseClosest(redDoors, ref player, out _);
        }
        else if (player.inventory == Inventory.KeyGreen && tilePositions.TryGetValue(Tile.DoorGreen, out var greenDoors))
        {
            player.action = UseClosest(greenDoors, ref player, out _);
        }
        else if (player.inventory == Inventory.KeyBlue && tilePositions.TryGetValue(Tile.DoorBlue, out var blueDoors))
        {
            player.action = UseClosest(blueDoors, ref player, out _);
        }
    }

    private void Attack(ref Player player)
    {
        if (!player.CanAct) return;

        if (!tilePositions.TryGetValue(Tile.Enemy, out var enemies)) return; // Are there enemies to attack?

        // Do we have a sword and enough health?
        var canAttack = player.hasSword && player.health > 1;
        if (canAttack)
        {
            player.action = UseClosest(enemies, ref player, out _);
        }
        else
        {
            // Are we close to an enemy?
            var distances = player.distances;
            var closestAdjacentEnemyPos = AdjacentPositions(enemies).OrderBy(p => distances[p]).First();
            var distanceToEnemy = distances[closestAdjacentEnemyPos];
            if (distanceToEnemy > 0) return;

            // Find empty position with largest distance
            var runAwayPos = tilePositions[Tile.Empty].OrderByDescending(p => DistanceBetween(p, closestAdjacentEnemyPos)).First();
            player.action = MoveTo(runAwayPos, ref player);
        }
    }

    private void PickupBoulder(ref Player player)
    {
        if (!player.CanAct || player.inventory != Inventory.None) return;

        if (tilePositions.TryGetValue(Tile.Boulder, out var boulderPositions))
        {
            // Remove boulders that should remain on a plate
            boulderPositions.ExceptWith(platesWithBoulders.Keys);
            if (boulderPositions.Count > 0)
            {
                player.action = UseClosest(boulderPositions, ref player, out _);
            }
        }
    }

    private void Explore(ref Player player)
    {
        if (!player.CanAct) return;

        var closestUnknown = GetClosestUnknown(player.distances);
        if (closestUnknown.HasValue)
        {
            player.action = MoveTo(closestUnknown.Value, ref player);
        }
    }

    private void MoveToPlate(ref Player player, HashSet<PosIndex> platePositions)
    {
        if (!player.CanAct || platePositions.Count == 0) return;

        // No plates to stand on before level 9
        if (level < 9) return;

        int closestPlate = FindClosesPositionFromPlayer(platePositions, ref player);
        if (closestPlate >= 0)
        {
            player.action = MoveTo(closestPlate, ref player);
            if (player.action != DirectedAction.None) player.targetPlatePosition = closestPlate;
        }
    }

    private void WaitAtPressurePlateDoor(ref Player player)
    {
        if (!player.CanAct) return;

        // Only when all plates are covered
        if (HasTiles(Tile.PressurePlateRed) || HasTiles(Tile.PressurePlateGreen) || HasTiles(Tile.PressurePlateBlue)) return;

        var closedDoors = plateDoorPositions.Where(p => map[p] != Tile.Empty).ToList();
        player.action = MoveToClosest(AdjacentPositions(closedDoors), ref player);
    }

    private void PlaceBoulderOnPlate(ref Player player, HashSet<PosIndex> platePositions)
    {
        if (!player.CanAct || player.inventory != Inventory.Boulder) return;
        if (platePositions.Count == 0) return;

        // Place boulder on plate
        player.action = UseClosest(platePositions, ref player, out var usedPosition);
        if (usedPosition.HasValue)
        {
            // And move away afterwards to see if anything changed
            if (player.action == DirectedAction.UseEast && player.CanMoveSouth) player.nextAction = DirectedAction.MoveSouth;
            else if (player.action == DirectedAction.UseEast && player.CanMoveNorth) player.nextAction = DirectedAction.MoveNorth;
            else if (player.action == DirectedAction.UseSouth && player.CanMoveEast) player.nextAction = DirectedAction.MoveEast;
            else if (player.action == DirectedAction.UseSouth && player.CanMoveWest) player.nextAction = DirectedAction.MoveWest;

            platesWithBoulders[usedPosition.Value] = 100;
        }
    }

    private void WalkRandom(ref Player player)
    {
        if (!player.CanAct) return;

        if (tilePositions.TryGetValue(Tile.Empty, out var emptyPositions))
        {
            var randomPos = emptyPositions.OrderBy(_ => Random.Shared.Next()).First();
            player.action = MoveTo(randomPos, ref player);
        }
    }


    private DirectedAction MoveTo(PosIndex toPos, ref Player player)
    {
        if (player.paths[toPos] < 0) return DirectedAction.None; // No path to this destination

        var route = new List<PosIndex>();
        var routePos = toPos;
        route.Add(routePos);
        while (routePos != player.pos)
        {
            routePos = player.paths[routePos];
            route.Add(routePos);
        }

        var adjacentPos = route[^2];

        return GetMoveDirection(player.pos, adjacentPos);
    }

    private DirectedAction MoveToClosest(IEnumerable<PosIndex> toPositions, ref Player player)
    {
        int closestPos = FindClosesPositionFromPlayer(toPositions, ref player);
        if (closestPos < 0) return DirectedAction.None; // No position reachable

        return MoveTo(closestPos, ref player);
    }

    private DirectedAction UseClosest(IEnumerable<PosIndex> toPositions, ref Player player, out PosIndex? usedPosition)
    {
        // Standing next to any of the positions?
        foreach (var pos in toPositions)
        {
            if (AreAdjacent(player.pos, pos))
            {
                usedPosition = pos;
                return GetUseDirection(player.pos, pos);
            }
        }
        usedPosition = null;

        // Move to closest position adjacent to target position
        return MoveToClosest(AdjacentPositions(toPositions), ref player);
    }

    private static int FindClosesPositionFromPlayer(IEnumerable<int> positions, ref Player player)
    {
        var closestPos = -1;
        var closestDist = int.MaxValue;

        foreach (var pos in positions)
        {
            var dist = player.distances[pos];
            if (dist < closestDist)
            {
                closestDist = dist;
                closestPos = pos;
            }
        }

        return closestPos;
    }

    private DirectedAction GetMoveDirection(PosIndex fromPos, PosIndex toPos)
    {
        var delta = toPos - fromPos;
        if (delta == 1) return DirectedAction.MoveEast;
        if (delta == -1) return DirectedAction.MoveWest;
        if (delta == mapWidth) return DirectedAction.MoveSouth;
        if (delta == -mapWidth) return DirectedAction.MoveNorth;
        return DirectedAction.None;
    }

    private DirectedAction GetUseDirection(PosIndex fromPos, PosIndex toPos)
    {
        var delta = toPos - fromPos;
        if (delta == 1) return DirectedAction.UseEast;
        if (delta == -1) return DirectedAction.UseWest;
        if (delta == mapWidth) return DirectedAction.UseSouth;
        if (delta == -mapWidth) return DirectedAction.UseNorth;
        return DirectedAction.None;
    }

    private PosIndex? GetClosestUnknown(int[] distances)
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

    private void UpdateState(State state)
    {
        if (state.Level != level)
        {
            Array.Fill(map, Tile.Unknown);
            tilePositions.Clear();
            plateDoorPositions.Clear();
            keyPickupPositions.Clear();
            platesWithBoulders.Clear();
            player1.Reset();
            player2.Reset();
            level21State = 0;
            level22State = 0;
            bossPos = -1;
            Console.WriteLine($"Entered level {state.Level}");
        }
        level = state.Level;
        tick = state.Tick;
        Debug.WriteLine($"---- tick {tick} level {level} ----");

        player1.SyncFromState(state.PlayerState);
        player2.SyncFromState(state.Player2State);
        Debug.WriteLine($"pos: {player1.y},{player1.x} / {player2.y},{player2.x}");
        Debug.WriteLine($"inventory: {player1.inventory} / {player2.inventory}");
        Debug.WriteLine($"health: {player1.health} / {player2.health}");
        Debug.WriteLine($"hasSword: {player1.hasSword} / {player2.hasSword}");
        Debug.WriteLine($"nextAction: {player1.nextAction} / {player2.nextAction}");

        if (state.PlayerState != null) CopyPlayerStateToMap(state.PlayerState);
        if (state.Player2State != null) CopyPlayerStateToMap(state.Player2State);

        // Update locations where keys can be picked up.
        // This information this used by ComputePaths
        UpdateKeyPickups();

        ComputePaths(ref player1);
        ComputePaths(ref player2);
    }

    private void CopyPlayerStateToMap(PlayerState playerState)
    {
        if (playerState.Surroundings.Count == 0) return;
        var playerPosY = playerState.Position.Y;
        var playerPosX = playerState.Position.X;

        var surroundingsIndex = 0;
        for (var dy = -visibilityRange; dy <= visibilityRange; dy++)
        {
            for (var dx = -visibilityRange; dx <= visibilityRange; dx++)
            {
                var tile = playerState.Surroundings[surroundingsIndex++];

                if (tile == Tile.Unknown) continue;

                var y = playerPosY + dy;
                var x = playerPosX + dx;
                if (y < 0 || y >= mapHeight || x < 0 || x >= mapWidth) continue;

                var posIndex = y * mapWidth + x;

                SetTile(posIndex, tile);
            }
        }
    }

    private void SetTile(int posIndex, Tile tile)
    {
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

    private void UpdateKeyPickups()
    {
        // Find positions of keys that have a door
        keyPickupPositions.Clear();
        if (HasTiles(Tile.DoorRed) && tilePositions.TryGetValue(Tile.KeyRed, out var rp))
        {
            Debug.WriteLine($"> Red key at {rp.First()}");
            keyPickupPositions.AddRange(rp);
        }
        if (HasTiles(Tile.DoorGreen) && tilePositions.TryGetValue(Tile.KeyGreen, out var gp))
        {
            Debug.WriteLine($"> Green key at {gp.First()}");
            keyPickupPositions.AddRange(gp);
        }
        if (HasTiles(Tile.DoorBlue) && tilePositions.TryGetValue(Tile.KeyBlue, out var bp))
        {
            Debug.WriteLine($"> Blue key at {bp.First()}");
            keyPickupPositions.AddRange(bp);
        }
    }

    private void ComputePaths(ref Player player)
    {
        var distances = player.distances;
        var paths = player.paths;
        var hasSword = player.hasSword;
        var inventory = player.inventory;
        var canPickupKeys = keyPickupPositions.Count > 0;

        Array.Fill(distances, int.MaxValue);
        Array.Fill(paths, -1);

        var todo = new Queue<PosIndex>();

        // Only proceed when start position is valid
        if (player.pos >= 0)
        {
            todo.Enqueue(player.pos);
            distances[player.pos] = 0;
        }

        void Enqueue(PosIndex curPos, int curDist, PosIndex nextPos)
        {
            if (IsWall(nextPos, inventory, hasSword, canPickupKeys)) return;

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

    private bool IsWall(PosIndex pos, Inventory inventory, bool hasSword, bool canPickupKeys)
    {
        // Avoid moving to close to the boss
        if (level == 22 && bossPos >= 0 && level22State < 1)
        {
            if (DistanceBetween(pos, bossPos) < 6) return true;
        }
        // Avoid moving to close to the enemy
        if (level == 21 && level21State < 8 && tilePositions.TryGetValue(Tile.Enemy, out var enemies))
        {
            var distanceToEnemy = enemies.Min(e => DistanceBetween(pos, e));
            if (distanceToEnemy < 6) return true;
        }

        var tile = map[pos];
        return tile switch
        {
            Tile.Unknown => true,
            Tile.Wall => true,
            Tile.DoorRed => true,
            Tile.DoorGreen => true,
            Tile.DoorBlue => true,
            Tile.Boulder => true,
            Tile.Player => true,
            Tile.Enemy => true,
            Tile.Boss => true,
            Tile.Sword => hasSword,
            Tile.KeyRed => inventory != Inventory.None || !canPickupKeys,
            Tile.KeyGreen => inventory != Inventory.None || !canPickupKeys,
            Tile.KeyBlue => inventory != Inventory.None || !canPickupKeys,
            Tile.Treasure => inventory != Inventory.None || level22State < 6,
            _ => false,
        };
    }

    private double DistanceBetween(PosIndex a, PosIndex b)
    {
        var dx = (a % mapWidth) - (b % mapWidth);
        var dy = (a / mapWidth) - (b / mapWidth);
        return Math.Sqrt(dx * dx + dy * dy);
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
                    Tile.Unknown => $"{ConsoleEx.GRAY}.·{ConsoleEx.RESET}",
                    Tile.Empty => "  ",
                    Tile.Wall => $"{ConsoleEx.WHITE}██{ConsoleEx.RESET}",
                    Tile.Player => $"{ConsoleEx.BRIGHT_CYAN}{PlayerId(posIndex)}{ConsoleEx.RESET}",
                    Tile.Exit => $"{ConsoleEx.BRIGHT_MAGENTA}><{ConsoleEx.RESET}",
                    Tile.DoorRed => $"{ConsoleEx.RED}##{ConsoleEx.RESET}",
                    Tile.DoorGreen => $"{ConsoleEx.GREEN}##{ConsoleEx.RESET}",
                    Tile.DoorBlue => $"{ConsoleEx.BLUE}##{ConsoleEx.RESET}",
                    Tile.KeyRed => $"{ConsoleEx.RED}=${ConsoleEx.RESET}",
                    Tile.KeyGreen => $"{ConsoleEx.GREEN}=${ConsoleEx.RESET}",
                    Tile.KeyBlue => $"{ConsoleEx.BLUE}=${ConsoleEx.RESET}",
                    Tile.Boulder => $"{ConsoleEx.BRIGHT_WHITE}(){ConsoleEx.RESET}",
                    Tile.PressurePlateRed => $"{ConsoleEx.RED}[]{ConsoleEx.RESET}",
                    Tile.PressurePlateGreen => $"{ConsoleEx.GREEN}[]{ConsoleEx.RESET}",
                    Tile.PressurePlateBlue => $"{ConsoleEx.BLUE}[]{ConsoleEx.RESET}",
                    Tile.Enemy => $"{ConsoleEx.BRIGHT_YELLOW}E!{ConsoleEx.RESET}",
                    Tile.Sword => $"{ConsoleEx.BRIGHT_CYAN}!!{ConsoleEx.RESET}",
                    Tile.Health => $"{ConsoleEx.BRIGHT_MAGENTA}++{ConsoleEx.RESET}",
                    Tile.Boss => $"{ConsoleEx.BRIGHT_YELLOW}B!{ConsoleEx.RESET}",
                    Tile.Treasure => $"{ConsoleEx.BRIGHT_YELLOW}$${ConsoleEx.RESET}",
                    _ => "??"
                });
            }
            Console.WriteLine(row.ToString());
            row.Clear();
        }

        Console.WriteLine($"---- tick {tick} level {level} ----");
        Console.WriteLine($"pos: {player1.y},{player1.x} / {player2.y},{player2.x},  " +
            $"inventory: {player1.inventory} / {player2.inventory},  " +
            $"health: {player1.health} / {player2.health},  " +
            $"hasSword: {player1.hasSword} / {player2.hasSword},  " +
            $"remainOnPlate: {player1.remainOnPlate} / {player2.remainOnPlate}        ");
        if (level == 22)
        {
            Console.WriteLine($"level 22: {level22State}        ");
        }
        else if (level == 21)
        {
            Console.WriteLine($"level 21: {level21State}        ");
        }
        Console.WriteLine($"action: {player1.action} / {player2.action},  next: {player1.nextAction} / {player2.nextAction}        ");
    }

    private string PlayerId(PosIndex pos)
    {
        if (pos == player1.pos) return "P1";
        if (pos == player2.pos) return "P2";
        return "P?";
    }

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

    private bool HasTiles(Tile tile) => tilePositions.ContainsKey(tile);
}
