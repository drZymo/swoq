namespace Swoq.Server.Models;

public class SwoqDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;

    public string DatabaseName { get; set; } = null!;

    public string PlayersCollectionName { get; set; } = null!;
}
