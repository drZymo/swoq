using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Data;

namespace Swoq.Server.Services;

internal class UserService(ISwoqDatabase database) : Interface.UserService.UserServiceBase
{
    public override async Task<Scores> GetScores(Empty request, ServerCallContext context)
    {
        var users = await database.GetAllUsers();

        var scores = new Scores();
        scores.Scores_.AddRange(users.Select(u => new Score()
        {
            UserName = u.Name,
            Level = u.Level,
            LengthTicks = u.QuestLengthTicks,
            LengthSeconds = u.QuestLengthSeconds
        }));
        return scores;
    }
}
