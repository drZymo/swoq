using Swoq.Server.Models;
using Swoq.Server.Services;

namespace Swoq.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();

        builder.Services.Configure<SwoqDatabaseSettings>(builder.Configuration.GetSection("SwoqDatabase"));
        builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabase>();
        //builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabaseInMemory>();
        builder.Services.AddSingleton<TrainingServer>();
        builder.Services.AddSingleton<QuestServer>();

        var app = builder.Build();
        app.MapGrpcService<PlayerService>();
        app.MapGrpcService<TrainingService>();
        app.MapGrpcService<QuestService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        app.Run();
    }
}