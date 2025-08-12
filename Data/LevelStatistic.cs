using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Swoq.Data;

public class LevelStatistic
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public int Level { get; set; } = 0;

    public int Ticks { get; set; } = int.MaxValue;

    public int Seconds { get; set; } = int.MaxValue;
}
