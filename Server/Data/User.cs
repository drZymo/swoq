using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Swoq.Server.Data;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = null!;

    public int Level { get; set; } = 0;

    public int QuestLengthTicks { get; set; } = int.MaxValue;

    public int QuestLengthSeconds { get; set; } = int.MaxValue;
}
