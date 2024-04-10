using System.Collections.Immutable;
using System.Numerics;
using System.Xml.Linq;

namespace Swoc2024Server;

internal record GameState(IImmutableDictionary<Position, Cell> Cells, IImmutableDictionary<Guid, Player> Players)
{
    public GameState SetEmpty(Position position)
    {
        return this with
        {
            Cells = Cells.SetItem(position, new EmptyCell()),
            Updates = Updates.Add(new CellUpdate(position))
        };
    }

    public GameState SetFood(Position position, int foodValue)
    {
        return this with
        {
            Cells = Cells.SetItem(position, new FoodCell(foodValue)),
            Updates = Updates.Add(new CellUpdate(position, FoodValue: foodValue))
        };
    }

    public GameState SetSnake(Position position, Snake snake)
    {
        return this with
        {
            Cells = Cells.SetItem(position, new SnakeRefCell(snake.PlayerId, snake.Name)),
            Updates = Updates.Add(new CellUpdate(position, PlayerName: Players[snake.PlayerId].Name))
        };
    }

    public IImmutableList<CellUpdate> Updates { get; init; } = ImmutableList<CellUpdate>.Empty;

    public IEnumerable<CellUpdate> GetCellUpdates()
    {
        var updates = ImmutableList<CellUpdate>.Empty;

        foreach (var (position, cell) in Cells)
        {
            if (cell is EmptyCell)
            {
                updates = updates.Add(new CellUpdate(position));
            }
            else if (cell is FoodCell f)
            {
                var foodValue = f.Value;
                updates = updates.Add(new CellUpdate(position, FoodValue: foodValue));
            }
            else if (cell is SnakeRefCell s)
            {
                var player = Players[s.PlayerId];
                var snake = player.Snakes[s.SnakeName];
                updates = updates.Add(new CellUpdate(position, PlayerName: player.Name));
            }
        }

        return updates;
    }

}
