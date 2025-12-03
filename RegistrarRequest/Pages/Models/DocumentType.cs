using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class DocumentType
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("documentTypeId")]
        public int DocumentTypeId { get; set; }

        [BsonElement("documentName")]
        public string DocumentName { get; set; } = string.Empty;

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("requiresPayment")]
        public bool RequiresPayment { get; set; }

        [BsonElement("amount")]
        public decimal Amount { get; set; }

        [BsonElement("processingDays")]
        public int ProcessingDays { get; set; }

        [BsonElement("requiresClearance")]
        public bool RequiresClearance { get; set; }

        [BsonElement("category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;
    }
}
