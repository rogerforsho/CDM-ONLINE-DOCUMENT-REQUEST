using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectCapstone.Models
{
    public class Payment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("paymentId")]
        public int PaymentId { get; set; }

        [BsonElement("requestId")]
        public int RequestId { get; set; }

        [BsonElement("amount")]
        public decimal Amount { get; set; }

        [BsonElement("paymentMethod")]
        public string PaymentMethod { get; set; } = string.Empty;

        [BsonElement("referenceNumber")]
        public string? ReferenceNumber { get; set; }

        [BsonElement("paymentProofUrl")]
        public string? PaymentProofUrl { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = "Pending";

        [BsonElement("verifiedBy")]
        public int? VerifiedBy { get; set; }

        [BsonElement("verifiedDate")]
        public DateTime? VerifiedDate { get; set; }

        [BsonElement("rejectionReason")]
        public string? RejectionReason { get; set; }

        [BsonElement("paymentDate")]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedDate")]
        public DateTime? UpdatedDate { get; set; }
    }
}
