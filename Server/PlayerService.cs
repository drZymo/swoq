using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Services;

namespace Swoq.Server;

internal class PlayerService(ILogger<PlayerService> logger, ISwoqDatabase database) : Player.PlayerBase
{
    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var existingPlayer = await database.FindPlayerByNameAsync(request.PlayerName);
        if (existingPlayer != null)
        {
            return new RegisterResponse { Result = Result.PlayerAlreadyRegistered };
        }

        var player = new Models.Player { Name = request.PlayerName };
        await database.CreatePlayerAsync(player);
        logger.LogInformation("Player {Name} registered with id {Id}", player.Name, player.Id);
        return new RegisterResponse { Result = Result.Ok, PlayerId = player.Id };
    }
}
