using Swoq.Data;
using Swoq.Infra;
using Swoq.Server;
using Swoq.Server.Data;
using Swoq.Server.Services;
using System.Collections.Immutable;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:5001");
builder.Services.AddGrpc();

builder.Services.Configure<SwoqDatabaseSettings>(builder.Configuration.GetSection("SwoqDatabase"));
builder.Services.Configure<ReplayStorageSettings>(builder.Configuration.GetSection("ReplayStorage"));
builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabase>();
//builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabaseInMemory>();
builder.Services.AddSingleton<IMapGenerator, MapGenerator>();
builder.Services.AddSingleton<GameServicePostman>();
builder.Services.AddSingleton<ReplaySaver>();
if (args.Contains("--final"))
{
    Console.WriteLine($"{ConsoleColors.BrightRed}!! Running in FINAL MODE !!{ConsoleColors.Reset}");
    Console.WriteLine("Enter user names of finalists (one per line, empty line to start):");
    var userNames = ImmutableHashSet<string>.Empty;
    while (true)
    {
        Console.Write("> ");
        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) break;
        userNames = userNames.Add(line);
    }

    builder.Services.AddSingleton<IGameServer, FinalGameServer>(sp => new FinalGameServer(sp.GetRequiredService<IMapGenerator>(), sp.GetRequiredService<ISwoqDatabase>(), userNames));
}
else
{
    builder.Services.AddSingleton<IGameServer, GameServer>();
}

var app = builder.Build();
app.MapGrpcService<GameService>();
app.MapGrpcService<DashboardService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

// force some services to be created
_ = app.Services.GetService<ReplaySaver>();
_ = app.Services.GetService<IGameServer>();

app.Run();
