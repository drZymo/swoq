using System.Collections.Immutable;
using System.Diagnostics;

namespace Swoq.Server;

using Position = (int y, int x);

internal class Game
{
    private const int VisibilityRange = 4;

    private const int Width = 20;
    private const int Height = 20;

    private Cell[,] map;

    private Position playerPos;

    public Game(Guid id)
    {
        // Created walled square
        map = new Cell[Height, Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                map[y, x] = new Cell(CellType.Empty, false);
            }
        }
        for (var x = 0; x < Width; x++)
        {
            map.ChangeCellType((0, x), CellType.Wall);
            map.ChangeCellType((Height - 1, x), CellType.Wall);
        }
        for (var y = 1; y < Height - 1; y++)
        {
            map.ChangeCellType((y, 0), CellType.Wall);
            map.ChangeCellType((y, Width - 1), CellType.Wall);
        }

        // Exit bottom right
        map.ChangeCellType((Height - 2, Width - 2), CellType.Exit);

        // Start top left
        playerPos = (1, 1);

        UpdateVisibility();

        Debug.Assert(map[playerPos.y, playerPos.x].Type == CellType.Empty);
        Id = id;
    }

    public Guid Id { get; }

    public int[] GetState()
    {
        var state = new int[Height * Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                state[y * Width + x] = GetCellState((y, x));
            }
        }
        return state;
    }

    private int GetCellState(Position pos)
    {
        if (pos.Equals(playerPos))
        {
            return 1; // STATE_PLAYER
        }

        var cell = map[pos.x, pos.y];
        if (cell.IsVisible)
        {
            switch (cell.Type)
            {
                case CellType.Empty: return 2; // STATE_EMPTY
                case CellType.Wall: return 3; // STATE_WALL
                case CellType.Exit: return 4; // STATE_EXIT
            }
        }

        return 0; // STATE_UNKOWN
    }

    private void UpdateVisibility()
    {
        var minY = Math.Clamp(playerPos.y - VisibilityRange, 0, Height - 1);
        var maxY = Math.Clamp(playerPos.y + VisibilityRange, 0, Height - 1);
        var minX = Math.Clamp(playerPos.x - VisibilityRange, 0, Width - 1);
        var maxX = Math.Clamp(playerPos.x + VisibilityRange, 0, Width - 1);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var pos = (y, x);
                if (pos.DistanceTo(playerPos) < VisibilityRange)
                {
                    map.MakeVisible(pos);
                }
            }
        }
    }

}

//namespace Swoc2024Server;

//internal class Game : IDisposable, IGame, ICellUpdater
//{
//    private static Random random = new();

//    public const int TickPeriodMs = 100; // ms

//    private readonly int width;
//    private readonly int height;

//    private readonly object stateMutex = new();
//    private GameState nextState = new GameState(ImmutableDictionary<Position, Cell>.Empty, ImmutableDictionary<Guid, Player>.Empty);
//    private GameState currentState;

//    private readonly Timer timer;

//    public Game(int width, int height, int nrFood)
//    {
//        this.width = width;
//        this.height = height;

//        // Create empty field
//        var cells = nextState.Cells;
//        for (var y = 0; y < height; y++)
//        {
//            for (var x = 0; x < width; x++)
//            {
//                cells = cells.Add(new Position([y, x]), new EmptyCell());
//            }
//        }
//        nextState = nextState with { Cells = cells };

//        // Add food
//        var foodPositions = cells.Keys.OrderBy(_ => random.Next()).Take(nrFood).ToList();
//        foreach (var position in foodPositions)
//        {
//            nextState = nextState.SetFood(position, 1);
//        }

//        // Make this the current state
//        currentState = nextState with { Updates = ImmutableList<CellUpdate>.Empty };

//        Console.WriteLine("Game created");

//        timer = new System.Threading.Timer(OnTimer, null, 0, TickPeriodMs);
//    }

//    public void Dispose()
//    {
//        timer.Dispose();
//    }

//    #region IGame

//    public IEnumerable<CellUpdate> GetState()
//    {
//        return currentState.GetCellUpdates();
//    }

//    public GameSettings Register(string playerName)
//    {
//        Console.WriteLine($"Register: {playerName}");

//        lock (stateMutex)
//        {
//            // Remove old players with same name
//            var player = nextState.Players.Values.FirstOrDefault(p => p.Name == playerName);
//            if (player != null)
//            {
//                nextState = nextState with { Players = nextState.Players.Remove(player.Id) };
//            }

//            // Create new player at random start position
//            var startPosition = nextState.Cells.Where(c => c.Value is EmptyCell).OrderBy(_ => random.Next()).First().Key;
//            var playerId = Guid.NewGuid();
//            player = new Player(playerName, playerId, startPosition, ImmutableDictionary<string, Snake>.Empty, 0);

//            // Create initial snake
//            var snake = new Snake(player.Name, player.Id, startPosition, this);
//            player = player with { Snakes = player.Snakes.Add(snake.Name, snake) };

//            // Add to game
//            nextState = nextState with { Players = nextState.Players.Add(player.Id, player) };
//            SetSnake(startPosition, snake);
//            Console.WriteLine($"New snake: {snake.Name} at Y:{snake.HeadPosition[0]}, X:{snake.HeadPosition[1]}");

//            return new GameSettings(player.Id, [height, width], startPosition);
//        }
//    }

//    public void Move(Guid playerId, string snakeName, Position nextPosition)
//    {
//        try
//        {
//            lock (stateMutex)
//            {
//                // Find corresponding snake
//                var player = nextState.Players[playerId];
//                var snake = player.Snakes[snakeName];

//                // Determine axis and direction to move
//                var (axis, direction) = GetMovementDirection(snake, nextPosition);
//                if (axis == -1 || direction == 0) return;

//                // Move to next cell
//                var nextCell = nextState.Cells[nextPosition];
//                if (nextCell is EmptyCell)
//                {
//                    snake.Move(axis, direction);
//                }
//                else if (nextCell is FoodCell food)
//                {
//                    snake.Grow(axis, direction);

//                    // Increase player score
//                    player = player with { Score = player.Score + food.Value };
//                    nextState = nextState with { Players = nextState.Players.SetItem(player.Id, player) };
//                    Console.WriteLine($"Snake {snake.Name} of player {player.Name} ate food of value {food.Value}. Player score is now {player.Score}.");
//                }
//                else if (nextCell is SnakeRefCell)
//                {
//                    // TODO: kill
//                }
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.Error.WriteLine($"Move({playerId}, {snakeName}, {nextPosition}) ignored. Exception {ex}");
//        }
//    }

//    private static (int axis, int direction) GetMovementDirection(Snake snake, Position nextPosition)
//    {
//        var headPosition = snake.HeadPosition;

//        if (nextPosition.Values.Count != headPosition.Values.Count)
//        {
//            Console.Error.WriteLine($"Move({snake.PlayerId}, {snake.Name}, {nextPosition}) ignored. Wrong number of dimensions.");
//            return (-1, 0);
//        }

//        int axis = -1;
//        int direction = 0;
//        for (int i = 0; i < headPosition.Values.Count; i++)
//        {
//            var diff = nextPosition[i] - headPosition[i];
//            if (diff != 0)
//            {
//                if (Math.Abs(diff) > 1)
//                {
//                    Console.Error.WriteLine($"Move({snake.PlayerId}, {snake.Name}, {nextPosition}) ignored. Distance too big.");
//                    return (-1, 0);
//                }
//                if (axis != -1 || direction != 0)
//                {
//                    Console.Error.WriteLine($"Move({snake.PlayerId}, {snake.Name}, {nextPosition}) ignored. Diagonal move.");
//                    return (-1, 0);
//                }
//                axis = i;
//                direction = diff > 0 ? 1 : -1;
//            }
//        }
//        Debug.Assert(0 <= axis && axis < headPosition.Values.Count);
//        Debug.Assert(direction == -1 || direction == 1);
//        return (axis, direction);
//    }

//    public event EventHandler<GameUpdate>? Updated;

//    public event EventHandler? Finished;

//    #endregion

//    #region ICellUpdater

//    public void Clear(Position position)
//    {
//        nextState = nextState.SetEmpty(position);
//    }

//    public void SetFood(Position position, int foodValue)
//    {
//        nextState = nextState.SetFood(position, foodValue);
//    }

//    public void SetSnake(Position position, Snake snake)
//    {
//        nextState = nextState.SetSnake(position, snake);
//    }

//    #endregion

//    private void OnTimer(object? state)
//    {
//        try
//        {
//            Tick();
//        }
//        catch (Exception ex)
//        {
//            Console.Error.WriteLine($"OnTimer() exception {ex}");
//        }
//    }

//    private int prevFoodLeft = -1;

//    private void Tick()
//    {
//        IImmutableList<CellUpdate> cellUpdates;
//        IImmutableList<PlayerScore> playerScores;
//        lock (stateMutex)
//        {
//            cellUpdates = nextState.Updates;
//            playerScores = nextState.Players.Values.Select(p => new PlayerScore(p.Name, p.Snakes.Count, p.Score)).ToImmutableArray();
//            nextState = nextState with { Updates = ImmutableList<CellUpdate>.Empty };
//            currentState = nextState;
//        }

//        Updated?.Invoke(this, new GameUpdate(cellUpdates, playerScores));

//        var foodLeft = currentState.Cells.Values.OfType<FoodCell>().Count();
//        if (foodLeft != prevFoodLeft)
//        {
//            Console.WriteLine($"Food left: {foodLeft}");
//        }
//        prevFoodLeft = foodLeft;
//        if (foodLeft <= 0)
//        {
//            Finished?.Invoke(this, EventArgs.Empty);
//            timer.Dispose();
//        }
//    }
//}
