using Bot;
using Swoq.Interface;
using System.Diagnostics;
using System.Text;
using PosIndex = int;


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

    private readonly HashSet<PosIndex> enemyPositions = [];

    private int level21State = 0;
    private int level22State = 0;
    private PosIndex lastBossPos = -1;
    private PosIndex initialBossPos = -1;

    private const int BossDistance = 5;

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

        UpdatePlayers();
        StorePlateDoorPositions();
        StoreEnemyPositions();

        // Gather some information
        var canBothReachExit = false;
        if (player1.IsPresent && player2.IsPresent)
        {
            // Player1 can reach exit and player2 can reach player1 or vice versa
            canBothReachExit = (player1.CanReachExit && AdjacentPositions([player1.pos]).Any(p => player2.CanReach(p))) ||
                 (player2.CanReachExit && AdjacentPositions([player2.pos]).Any(p => player1.CanReach(p)));
        }
        else if (player1.IsPresent)
        {
            canBothReachExit = player1.CanReachExit;
        }
        else if (player2.IsPresent)
        {
            canBothReachExit = player2.CanReachExit;
        }

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

        // Check if current target can still be reached
        if (player.targetPlatePosition >= 0 && player.targetPlatePosition != player.pos && !player.CanReach(player.targetPlatePosition))
        {
            player.targetPlatePosition = -1;
            player.remainOnPlateCounter = -1;
        }
        if (player.targetPosition >= 0 && player.targetPosition != player.pos && !player.CanReach(player.targetPosition))
        {
            player.targetPosition = -1;
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
        if (player.pos == player1.pos && level != 20)
        {
            MoveToPlate(ref player, platePositions);
            Print("move to plate", ref player);
        }

        WaitAtPressurePlateDoor(ref player);
        Print("wait at door", ref player);

        PickupBoulder(ref player);
        Print("pickup boulder", ref player);

        WalkToEnemy(ref player);
        Print("to enemy", ref player);

        DesperateAttack(ref player);
        Print("desparate attack", ref player);

        WalkRandom(ref player);
        Print("random", ref player);
    }

    private void UpdatePlayers()
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

        player1.Update(ref player2);
        player2.Update(ref player1);

        // Step off plate when enemy can be crushed
        if (tilePositions.TryGetValue(Tile.Enemy, out var enemyPositions))
        {
            if (enemyPositions.Any(p => plateDoorPositions.Contains(p)))
            {
                player1.targetPlatePosition = -1;
                player1.remainOnPlateCounter = -1;
                player2.targetPlatePosition = -1;
                player2.remainOnPlateCounter = -1;
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

    private void StoreEnemyPositions()
    {
        if (tilePositions.TryGetValue(Tile.Enemy, out var ep))
        {
            foreach (var p in ep)
            {
                enemyPositions.Add(p);
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
            // Step of plate when other player is outside start area
            player.targetPlatePosition = -1;
            player.remainOnPlateCounter = -1;
        }

        // Prevent going back to the start and trip the pressure plate accidentally
        if ((player1.x >= 8 || player1.y >= 11) && (player2.x >= 8 || player2.y >= 11))
        {
            for (var y = 0; y < 11; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    SetTile(y * mapWidth + x, Tile.Wall);
                }
            }
        }
    }

    private void HandleLevel21(ref Player player, ref Player other, bool canBothReachExit, HashSet<int> platePositions)
    {
        if (level != 21) return;

        Debug.WriteLine($"level21 {level21State}");

        // Prevent going to the enemy area when not ready yet
        if (level21State < 8)
        {
            for (var y = mapHeight - 18; y < mapHeight; y++)
            {
                for (var x = mapWidth - 11; x < mapWidth; x++)
                {
                    SetTile(y * mapWidth + x, Tile.Wall);
                }
            }
        }

        if (level21State == 0) // Find boulder and plates
        {
            Explore(ref player);

            // Completely explore first
            if (player.action == DirectedAction.None && platePositions.Count == 2 && tilePositions.ContainsKey(Tile.Boulder))
            {
                level21State = 1;
            }

        }
        if (level21State == 1) // Move player1 to plate and player2 to boulder
        {
            if (player1.remainOnPlateCounter >= 0 && player2.inventory == Inventory.Boulder)
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
                player1.targetPlatePosition = -1;
                player1.remainOnPlateCounter = -1;
                // Move out of the way for player 2 to step on the plate
                var awayPos = AdjacentPositions([player1.pos]).OrderByDescending(p => DistanceBetween(p, player2.pos)).First();
                player1.action = MoveTo(awayPos, ref player1);
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
            if (player2.remainOnPlateCounter >= 0)
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
            // player 2 stays on plate
            if (player.pos == player2.pos)
            {
                if (player.remainOnPlateCounter >= 0)
                {
                    player.remainOnPlateCounter = 100;
                }
                else
                {
                    MoveToPlate(ref player, platePositions);
                }
            }

            if (player1.hasSword && DistanceBetween(player1.pos, player2.pos) < 5)
            {
                level21State = 6;
                player2.targetPlatePosition = -1;
                player2.remainOnPlateCounter = -1;
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
            else
            {
                WalkRandom(ref player);
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
                // Open up enemy area for explorarion
                for (var y = mapHeight - 18; y < mapHeight; y++)
                {
                    for (var x = mapWidth - 11; x < mapWidth; x++)
                    {
                        SetTile(y * mapWidth + x, Tile.Unknown);
                    }
                }
            }
        }
        if (level21State == 8) // explore and attack
        {
            Attack(ref player);
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

        Debug.WriteLine($"level22 {level22State}");

        if (tilePositions.TryGetValue(Tile.Boss, out var bossPositions))
        {
            lastBossPos = bossPositions.First();
            if (initialBossPos == -1)
            {
                initialBossPos = lastBossPos;
            }
        }

        // Avoid moving to close to the boss
        if (initialBossPos >= 0 && level22State < 1)
        {
            var bossY = initialBossPos / mapWidth;
            var bossX = initialBossPos % mapWidth;
            for (var y = bossY - BossDistance; y <= bossY + BossDistance; y++)
            {
                if (y < 0 || y >= mapHeight) continue;
                for (var x = bossX - BossDistance; x <= bossX + BossDistance; x++)
                {
                    if (x < 0 || x >= mapWidth) continue;
                    var p = y * mapWidth + x;
                    if (p == initialBossPos) continue;

                    SetTile(p, Tile.Wall);
                }
            }
        }


        if (level22State == 0) // move to plate
        {
            // First explore everything before stepping on plate
            Explore(ref player);
            Debug.WriteLine($"Explore {player.action}");
            // Done exploring?
            if (player.action == DirectedAction.None)
            {
                // Is this the player that is on the plate?
                if (player.remainOnPlateCounter >= 0)
                {
                    // Stay there
                    player.remainOnPlateCounter = 100;
                }
                else if (other.remainOnPlateCounter >= 0)
                {
                    // If the other player is on the plate
                    // Move to the door about to be opened
                    player.action = MoveToClosest(plateDoorPositions, ref player);
                    if (player.action == DirectedAction.None)
                    {
                        player.action = MoveToClosest(AdjacentPositions(plateDoorPositions), ref player);
                    }
                }

                // If other player is on the plate, and this player is at the door, then proceed to next phase
                if (other.remainOnPlateCounter >= 0 && plateDoorPositions.Contains(player.pos))
                {
                    level22State = 1;
                }

                MoveToPlate(ref player, platePositions);
            }
        }
        if (level22State == 1) // move toward boss
        {
            // Is this the player on the plate?
            if (player.remainOnPlateCounter >= 0)
            {
                // Remain there
                player.remainOnPlateCounter = 100;
            }
            else
            {
                // No, this is the player to lure the boss
                if (DistanceBetween(player.pos, lastBossPos) > 4)
                {
                    player.action = MoveToClosest(AdjacentPositions([lastBossPos]), ref player);
                }
                else
                {
                    level22State = 2;
                }
            }
        }
        if (level22State == 2 || level22State == 3) // move to plate with boss
        {
            // Is this the player on the plate?
            if (player.remainOnPlateCounter >= 0)
            {
                // Remain there
                player.remainOnPlateCounter = 100;
                // Unless we can crush the boss
                if (plateDoorPositions.Contains(lastBossPos))
                {
                    player.action = DirectedAction.MoveWest;
                    player.targetPlatePosition = -1;
                    player.remainOnPlateCounter = -1;

                    // Reset the whole boss area to make sure the treasure is explored
                    var bossY = initialBossPos / mapWidth;
                    var bossX = initialBossPos % mapWidth;
                    for (var y = bossY - BossDistance; y <= bossY + BossDistance; y++)
                    {
                        if (y < 0 || y >= mapHeight) continue;
                        for (var x = bossX - BossDistance; x <= bossX + BossDistance; x++)
                        {
                            if (x < 0 || x >= mapWidth) continue;
                            var p = y * mapWidth + x;

                            SetTile(p, Tile.Unknown);
                        }
                    }
                    foreach (var p in plateDoorPositions)
                    {
                        SetTile(p, Tile.Wall);
                    }

                    level22State = 4;
                    return;
                }
            }
            else
            {
                // No, this is the player to lure the boss
                if (DistanceBetween(player.pos, lastBossPos) < 3)
                {
                    // Boss is following;
                    if (level22State == 2)
                    {
                        // move to door 
                        player.action = MoveToClosest(plateDoorPositions, ref player);
                    }
                    else if (level22State == 3)
                    {
                        // move to other 
                        player.action = MoveToClosest(AdjacentPositions([other.pos]), ref player);
                    }
                }

                if (level22State == 2 && plateDoorPositions.Contains(player.pos))
                {
                    level22State = 3;
                }
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

        if (!player.hasSword) return; // Do we have a sword?
        if (!tilePositions.TryGetValue(Tile.Enemy, out var enemies)) return; // Are there enemies to attack?

        // Only attack if we have enough health
        if (player.health > 1)
        {
            player.action = UseClosest(enemies, ref player, out _);
        }
        else if (level > 12 && level != 20)
        {
            MoveAwayFromEnemies(ref player);
        }
    }

    private bool Player1CanReachPlayer2()
    {
        if (!player1.IsPresent) return false;
        if (!player2.IsPresent) return false;

        return AdjacentPositions([player2.pos]).Any(p => player1.CanReach(p));
    }

    private void MoveAwayFromEnemies(ref Player player)
    {
        if (!player.CanAct) return;

        // Are there enemies to run away from
        if (!tilePositions.TryGetValue(Tile.Enemy, out var enemies)) return;

        // Compute distance to enemies for each poitn around player
        var runAwayPos = AdjacentPositions([player.pos]).OrderByDescending(p => enemies.Min(e => DistanceBetween(p, e)));
        if (!runAwayPos.Any()) return;

        player.action = MoveTo(runAwayPos.First(), ref player);
    }

    private void DesperateAttack(ref Player player)
    {
        if (!player.CanAct) return;
        if (!player.hasSword) return; // Do we have a sword?
        if (!tilePositions.TryGetValue(Tile.Enemy, out var enemies)) return; // Are there enemies to attack?

        player.action = UseClosest(enemies, ref player, out _);
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

        if (player.targetPlatePosition < 0)
        {
            player.targetPlatePosition = FindClosesPositionFromPlayer(platePositions, ref player);
        }

        if (player.targetPlatePosition >= 0)
        {
            player.action = MoveTo(player.targetPlatePosition, ref player);
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

    private void WalkToEnemy(ref Player player)
    {
        if (!player.CanAct) return;

        // Pick a new target if available
        if (player.targetPosition < 0)
        {
            player.targetPosition = (enemyPositions.Count > 0)
                ? enemyPositions.PickOne()
                : -1;

            Debug.WriteLine($"WalkToEnemy: target position {player.targetPosition}");
        }

        // Move to target if set
        if (player.targetPosition >= 0)
        {
            player.action = MoveTo(player.targetPosition, ref player);
        }
    }

    private void WalkRandom(ref Player player)
    {
        if (!player.CanAct) return;

        // Pick a new target if available
        if (player.targetPosition < 0)
        {
            if (tilePositions.TryGetValue(Tile.Empty, out var emptyPositions))
            {
                var plyr = player;
                var reachable = emptyPositions.Where(p => plyr.CanReach(p));
                if (reachable.Any())
                {
                    player.targetPosition = reachable.PickOne();
                    Debug.WriteLine($"WalkRandom: target position {player.targetPosition}");
                }
            }
        }

        // Move to target if set
        if (player.targetPosition >= 0)
        {
            player.action = MoveTo(player.targetPosition, ref player);
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
            enemyPositions.Clear();
            level21State = 0;
            level22State = 0;
            lastBossPos = -1;
            initialBossPos = -1;
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
        Debug.WriteLine($"targetPlatePosition: {player1.targetPlatePosition} / {player2.targetPlatePosition}");
        Debug.WriteLine($"remainOnPlateCounter: {player1.remainOnPlateCounter} / {player2.remainOnPlateCounter}");
        Debug.WriteLine($"targetPosition: {player1.targetPosition} / {player2.targetPosition}");

        if (state.PlayerState != null) CopyPlayerStateToMap(state.PlayerState);
        if (state.Player2State != null) CopyPlayerStateToMap(state.Player2State);
        // Manually set player positions
        foreach (var p in tilePositions[Tile.Player])
        {
            SetTile(p, Tile.Empty);
        }
        if (player1.IsPresent) SetTile(player1.pos, Tile.Player);
        if (player2.IsPresent) SetTile(player2.pos, Tile.Player);

        // Update locations where keys can be picked up.
        // This information this used by ComputePaths
        UpdateKeyPickups();

        // Compute paths as if other player is not there
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
        if (level == 22 && lastBossPos >= 0 && level22State < 1)
        {
            var bossY = lastBossPos / mapWidth;
            var bossX = lastBossPos % mapWidth;
            var posY = pos / mapWidth;
            var posX = pos % mapWidth;
            var dy = bossY - posY;
            var dx = bossX - posX;
            if (Math.Abs(dy) <= BossDistance && Math.Abs(dx) <= BossDistance) return true;
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
            // Prevent accidental step on plate
            Tile.PressurePlateRed => level22State > 3,
            Tile.PressurePlateGreen => level22State > 3,
            Tile.PressurePlateBlue => level22State > 3,
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
            $"remainOnPlateCounter: {player1.remainOnPlateCounter} / {player2.remainOnPlateCounter}        ");
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
