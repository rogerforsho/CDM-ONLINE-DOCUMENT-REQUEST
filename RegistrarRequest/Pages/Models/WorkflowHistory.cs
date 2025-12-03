using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class WorkflowHistory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("requestId")]
        public int RequestId { get; set; }

        [BsonElement("stage")]
        public string Stage { get; set; } = string.Empty;

        [BsonElement("action")]
        public string Action { get; set; } = string.Empty;

        [BsonElement("comments")]
        public string? Comments { get; set; }

        [BsonElement("processedBy")]
        public int ProcessedBy { get; set; }

        [BsonElement("processedAt")]
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}
