using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class SessionLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public int UserId { get; set; }

        [BsonElement("ipAddress")]
        public string IpAddress { get; set; } = string.Empty;

        [BsonElement("userAgent")]
        public string UserAgent { get; set; } = string.Empty;

        [BsonElement("loginTime")]
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;
    }
}
