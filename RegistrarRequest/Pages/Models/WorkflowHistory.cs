namespace ProjectCapstone.Models
{
    public class WorkflowHistory
    {
        public int HistoryId { get; set; }
        public int RequestId { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string? Action { get; set; }
        public string? Comments { get; set; }
        public int? ProcessedBy { get; set; }
        public DateTime ProcessedDate { get; set; }
    }
}
