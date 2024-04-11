using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Services;

namespace Swoq.Server;

internal class PlayerService : Player.PlayerBase
{
    private readonly ILogger<PlayerService> logger;
    private readonly SwoqDatabase database;

    public PlayerService(ILogger<PlayerService> logger, SwoqDatabase database)
    {
        this.logger = logger;
        this.database = database;
    }

    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var existingPlayer = await database.FindByNameAsync(request.PlayerName);
        if (existingPlayer != null)
        {
            return new RegisterResponse { Result = Result.PlayerAlreadyRegistered };
        }

        var player = new Models.Player { Name = request.PlayerName };
        await database.CreateAsync(player);
        logger.LogInformation($"Player {player.Name} registered with id {player.Id}");
        return new RegisterResponse { Result = Result.Ok, PlayerId = player.Id };
    }
}
