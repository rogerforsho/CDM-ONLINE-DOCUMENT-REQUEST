namespace ProjectCapstone.Models
{
    public class DocumentType
    {
        public int DocumentTypeId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool RequiresPayment { get; set; }
        public decimal Amount { get; set; }
        public int ProcessingDays { get; set; }
        public bool RequiresClearance { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
