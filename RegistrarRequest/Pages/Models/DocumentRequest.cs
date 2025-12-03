namespace ProjectCapstone.Models
{
    public class DocumentRequest
    {
        public int RequestId { get; set; }
        public string StudentName { get; set; } = "";

        public int UserId { get; set; }
        public int DocumentTypeId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "Not Required";
        public DateTime RequestDate { get; set; }
        public DateTime? TargetReleaseDate { get; set; }
        public DateTime? ActualReleaseDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CurrentStage { get; set; } = "Submitted";
        public string QueueNumber { get; set; } = string.Empty;
        public int? ProcessedBy { get; set; }
        public DateTime? ProcessedDate { get; set; }

        // Navigation properties
        public DocumentType? DocumentTypeInfo { get; set; }
        public Payment? PaymentInfo { get; set; }

        // Added for notifications
        public string StudentEmail { get; set; } = string.Empty;
    }
}
