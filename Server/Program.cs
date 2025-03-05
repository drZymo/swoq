using Swoq.Data;
using Swoq.Infra;
using Swoq.Server;
using Swoq.Server.Data;
using Swoq.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:5080", "https://*:5443");
builder.Services.AddGrpc();

builder.Services.Configure<SwoqDatabaseSettings>(builder.Configuration.GetSection("SwoqDatabase"));
builder.Services.Configure<ReplayStorageSettings>(builder.Configuration.GetSection("ReplayStorage"));
builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabase>();
//builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabaseInMemory>();
builder.Services.AddSingleton<MapGenerator>();
builder.Services.AddSingleton<GameServicePostman>();
builder.Services.AddSingleton<ReplaySaver>();
builder.Services.AddSingleton<IGameServer, GameServer>();

var app = builder.Build();
app.MapGrpcService<GameService>();
app.MapGrpcService<DashboardService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

// force replay saver to be created
_ = app.Services.GetService<ReplaySaver>();

app.Run();
