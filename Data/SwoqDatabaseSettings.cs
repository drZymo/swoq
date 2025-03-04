namespace Swoq.Data;

public class SwoqDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string UsersCollectionName { get; set; } = null!;
    public string LevelStatisticsCollectionName { get; set; } = null!;
}
