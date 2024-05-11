using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Data;

namespace Swoq.Server.Services;

internal class PlayerService(ILogger<PlayerService> logger, ISwoqDatabase database) : Interface.PlayerService.PlayerServiceBase
{
    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var existingPlayer = await database.FindPlayerByNameAsync(request.PlayerName);
        if (existingPlayer != null)
        {
            return new RegisterResponse { Result = Result.PlayerAlreadyRegistered };
        }

        var player = new Player { Name = request.PlayerName };
        await database.CreatePlayerAsync(player);
        logger.LogInformation("Player {Name} registered with id {Id}", player.Name, player.Id);
        return new RegisterResponse { Result = Result.Ok, PlayerId = player.Id };
    }

    public override async Task<Scores> GetScores(Empty request, ServerCallContext context)
    {
        var players = await database.GetAllPlayers();

        var scores = new Scores();
        scores.Scores_.AddRange(players.Select(p => new Score()
        {
            PlayerName = p.Name,
            Level = p.Level,
            LengthTicks = p.QuestLengthTicks,
            LengthSeconds = p.QuestLengthSeconds
        }));
        return scores;
    }
}
