namespace ProjectCapstone.Models
{
    public class MongoDBSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string DocumentRequestsCollection { get; set; } = string.Empty;
        public string UsersCollection { get; set; } = string.Empty;
        public string SessionLogsCollection { get; set; } = string.Empty;
        public string DocumentTypesCollection { get; set; } = string.Empty;
        public string WorkflowHistoryCollection { get; set; } = string.Empty;
        public string PaymentsCollection { get; set; } = string.Empty;
    }
}
