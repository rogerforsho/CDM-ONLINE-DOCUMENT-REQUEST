using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class DocumentRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("requestId")]
        public int RequestId { get; set; }

        [BsonElement("userId")]
        public int UserId { get; set; }

        [BsonElement("documentTypeId")]
        public int DocumentTypeId { get; set; }

        [BsonElement("queueNumber")]
        public string QueueNumber { get; set; } = string.Empty;

        [BsonElement("documentType")]
        public string DocumentType { get; set; } = string.Empty;

        [BsonElement("purpose")]
        public string Purpose { get; set; } = string.Empty;

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("totalAmount")]
        public decimal TotalAmount { get; set; }

        [BsonElement("paymentStatus")]
        public string PaymentStatus { get; set; } = "Not Required";

        [BsonElement("currentStage")]
        public string CurrentStage { get; set; } = "Pending Review";

        [BsonElement("status")]
        public string Status { get; set; } = "Active";

        [BsonElement("requestDate")]
        public DateTime RequestDate { get; set; }

        [BsonElement("targetReleaseDate")]
        public DateTime? TargetReleaseDate { get; set; }

        [BsonElement("completedDate")]
        public DateTime? CompletedDate { get; set; }

        [BsonElement("ReadyDate")]
        public DateTime? ReadyDate { get; set; }

        [BsonElement("notes")]
        public string? Notes { get; set; }

      

    }
}
