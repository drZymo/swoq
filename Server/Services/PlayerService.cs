using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Data;

namespace Swoq.Server.Services;

internal class PlayerService(ISwoqDatabase database) : Interface.PlayerService.PlayerServiceBase
{
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
