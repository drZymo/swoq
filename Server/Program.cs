using Swoc2024Server;
using Swoq.Server.Models;
using Swoq.Server.Services;

namespace Swoq.Server;

public class Program
{
    public static void Main(string[] args)
    {
        using var game = new Game(32, 32, 10);

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<IGame>(game);

        builder.Services.Configure<SwoqDatabaseSettings>(builder.Configuration.GetSection("SwoqDatabase"));
        builder.Services.AddSingleton<PlayersService>();

        var app = builder.Build();
        app.MapGrpcService<PlayerService>();
        app.MapGrpcService<TrainingService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        // Stop as soon as the game is finished
        game.Finished += async (s, e) => { await app.StopAsync(); };

        app.Run();
    }
}