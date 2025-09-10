using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Swoq.Data;

public class QuestProgress
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty!;

    public string GameId { get; set; } = string.Empty!;

    public int Level { get; set; } = 0;

    public int Ticks { get; set; } = int.MaxValue;

    public int Seconds { get; set; } = int.MaxValue;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
