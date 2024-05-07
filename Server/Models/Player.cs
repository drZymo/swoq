using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Swoq.Server.Models;

public class Player
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = null!;

    public int Level { get; set; } = 0;

    public int QuestLengthTicks { get; set; } = int.MaxValue;

    public TimeSpan QuestLengthTime { get; set; } = TimeSpan.MaxValue;
}
