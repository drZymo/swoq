using Swoq.Server.Models;
using Swoq.Server.Services;
using System.Text;

namespace Swoq.Server;

public class Program
{
    public static void Main(string[] args)
    {
        //var game = new Game();
        //var state = game.GetState();

        //for (int y = 0; y < 20; y++)
        //{
        //    var line = new StringBuilder();
        //    for (int x = 0; x < 20; x++)
        //    {
        //        char c = state[y * 20 + x] switch
        //        {
        //            0 => '·',
        //            1 => 'O',
        //            2 => ' ',
        //            3 => '#',
        //            4 => 'X',
        //            _ => '·',
        //        };
        //        line.Append(c);
        //    }
        //    Console.WriteLine(line.ToString());
        //}

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();

        //builder.Services.Configure<SwoqDatabaseSettings>(builder.Configuration.GetSection("SwoqDatabase"));
        //builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabase>();
        builder.Services.AddSingleton<ISwoqDatabase, SwoqDatabaseInMemory>();
        builder.Services.AddSingleton<TrainingServer>();

        var app = builder.Build();
        app.MapGrpcService<PlayerService>();
        app.MapGrpcService<TrainingService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        app.Run();
    }
}