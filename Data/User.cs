using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Swoq.Data;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Level { get; set; } = 0;

    public int QuestLengthTicks { get; set; } = int.MaxValue;

    public int QuestLengthSeconds { get; set; } = int.MaxValue;

    public bool QuestFinished { get; set; } = false;

    public string BestQuestId { get; set; } = string.Empty;
}
