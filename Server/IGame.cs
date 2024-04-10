using Swoc2024Server;

namespace Swoq.Server;

public record CellUpdate(Position Position, string? PlayerName = null, int? FoodValue = null);

public record GameSettings(Guid PlayerId, IEnumerable<int> Dimensions, Position StartPosition);

public record PlayerScore(string PlayerName, int NrSnakes, int Score);
public record GameUpdate(IEnumerable<CellUpdate> CellUpdates, IEnumerable<PlayerScore> PlayerScores);

public interface IGame
{
    IEnumerable<CellUpdate> GetState();
    GameSettings Register(string playerName);
    void Move(Guid playerId, string snakeName, Position nextPosition);

    event EventHandler<GameUpdate>? Updated;

    event EventHandler? Finished;
}
