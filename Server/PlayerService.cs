
using Grpc.Core;
using Swoq.Interface;
using Swoq.Server.Services;

namespace Swoq.Server;

internal class PlayerService : Swoq.Interface.Player.PlayerBase
{
    private readonly ILogger<PlayerService> logger;
    private readonly IGame game;
    private readonly PlayersService playersService;

    public PlayerService(ILogger<PlayerService> logger, IGame game, PlayersService playersService)
    {
        this.logger = logger;
        this.game = game;
        this.playersService = playersService;
    }

    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var existingPlayer = await playersService.FindByNameAsync(request.PlayerName);
        if (existingPlayer != null)
        {
            return new RegisterResponse { Result = Result.PlayerAlreadyRegistered };
        }

        var player = new Models.Player { Name = request.PlayerName };
        await playersService.CreateAsync(player);
        return new RegisterResponse { Result = Result.Ok, PlayerId = player.Id };
    }
}

//using Grpc.Core;
//using System.Collections.Concurrent;

//namespace Swoc2024Server;

//internal class PlayerService : PlayerHost.PlayerHostBase
//{
//    private readonly ILogger<PlayerService> logger;
//    private readonly IGame game;

//    public PlayerService(ILogger<PlayerService> logger, IGame game)
//    {
//        this.logger = logger;
//        this.game = game;
//    }

//    public override Task<GameStateMessage> GetGameState(EmptyRequest request, ServerCallContext context)
//    {
//        return Task.Run(() =>
//        {
//            var gameStateMessage = new PlayerInterface.GameStateMessage();
//            try
//            {
//                var updates = game.GetState().Select(u => u.ToUpdatedCell());
//                gameStateMessage.UpdatedCells.AddRange(updates);
//            }
//            catch (Exception ex)
//            {
//                logger.LogError($"GetGameState() exception {ex}");
//            }
//            return gameStateMessage;
//        });
//    }

//    public override Task<PlayerInterface.GameSettings> Register(RegisterRequest request, ServerCallContext context)
//    {
//        return Task.Run(() =>
//        {
//            var gameSettings = new PlayerInterface.GameSettings();
//            try
//            {
//                var settings = game.Register(request.PlayerName);
//                gameSettings = settings.ToGameSettings();
//            }
//            catch (Exception ex)
//            {
//                logger.LogError($"Register() exception {ex}");
//            }
//            return gameSettings;
//        });
//    }

//    public override Task Subscribe(SubsribeRequest request, IServerStreamWriter<GameUpdateMessage> responseStream, ServerCallContext context)
//    {
//        return Task.Run(() =>
//        {
//            try
//            {
//                var count = new SemaphoreSlim(0);
//                var queue = new ConcurrentQueue<GameUpdate>();

//                void OnGameUpdated(object? sender, GameUpdate update)
//                {
//                    queue.Enqueue(update);
//                    count.Release();
//                }
//                game.Updated += OnGameUpdated;

//                try
//                {
//                    while (!context.CancellationToken.IsCancellationRequested)
//                    {
//                        count.Wait(context.CancellationToken);
//                        if (queue.TryDequeue(out var update))
//                        {
//                            var message = update.ToGameUpdateMessage();
//                            responseStream.WriteAsync(message);
//                        }
//                    }
//                }
//                finally
//                {
//                    game.Updated -= OnGameUpdated;
//                }
//            }
//            catch (OperationCanceledException ex)
//            {
//                // Just exit
//            }
//            catch (Exception ex)
//            {
//                logger.LogError($"Subscribe() exception {ex}");
//            }
//        });
//    }

//    public override Task<EmptyRequest> MakeMove(Move request, ServerCallContext context)
//    {
//        return Task.Run(() =>
//        {
//            try
//            {
//                var playerId = Guid.Parse(request.PlayerIdentifier);
//                var nextPosition = new Position(request.NextLocation);
//                game.Move(playerId, request.SnakeName, nextPosition);
//            }
//            catch (Exception ex)
//            {
//                logger.LogError($"MakeMove() exception {ex}");
//            }
//            return new PlayerInterface.EmptyRequest();
//        });
//    }
//}
