namespace ProjectCapstone.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public int RequestId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? PaymentProofUrl { get; set; }
        public string Status { get; set; } = "Pending";
        public int? VerifiedBy { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? VerificationNotes { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
