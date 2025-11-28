using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using System.Data;

namespace ProjectCapstone.Pages.Dashboard
{
    public class StudentModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<StudentModel> _logger;

        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ReadyCount { get; set; }
        public int TotalRequests { get; set; }
        public List<DocumentRequest> RecentRequests { get; set; }
        public List<DocumentRequest> AllRequests { get; set; }
        public List<DocumentRequest> HistoryRequests { get; set; }

        public StudentModel(IConfiguration configuration, ILogger<StudentModel> logger)
        {
            _dbHelper = new DatabaseHelper(configuration);
            _logger = logger;
            RecentRequests = new List<DocumentRequest>();
            AllRequests = new List<DocumentRequest>();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Get user info from session
            FullName = HttpContext.Session.GetString("FullName");
            StudentNumber = HttpContext.Session.GetString("StudentNumber");

            try
            {
                // Get statistics
                await LoadStatistics(userId.Value);

                // Get recent requests (for dashboard)
                await LoadRecentRequests(userId.Value);

                // Get all requests (for My Requests page)
                await LoadAllRequests(userId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Dashboard loading error: {ex.Message}");
            }

            return Page();
        }

        private async Task LoadStatistics(int userId)
        {
            // Get pending count
            var pendingQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId AND Status = 'Pending'";
            var pendingParams = new Dictionary<string, object> { { "@UserId", userId } };
            PendingCount = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(pendingQuery, pendingParams));

            // Get processing count
            var processingQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId AND Status = 'Processing'";
            var processingParams = new Dictionary<string, object> { { "@UserId", userId } };
            ProcessingCount = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(processingQuery, processingParams));

            // Get ready count
            var readyQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId AND Status = 'Ready'";
            var readyParams = new Dictionary<string, object> { { "@UserId", userId } };
            ReadyCount = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(readyQuery, readyParams));

            // Get total requests
            var totalQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId";
            var totalParams = new Dictionary<string, object> { { "@UserId", userId } };
            TotalRequests = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(totalQuery, totalParams));
        }

        private async Task LoadRecentRequests(int userId)
        {
            var query = @"SELECT RequestId, QueueNumber, DocumentType, Purpose, RequestDate, Status 
                         FROM DocumentRequests 
                         WHERE UserId = @UserId 
                         ORDER BY RequestDate DESC 
                         LIMIT 5";

            var parameters = new Dictionary<string, object> { { "@UserId", userId } };
            var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

            foreach (DataRow row in result.Rows)
            {
                RecentRequests.Add(new DocumentRequest
                {
                    RequestId = Convert.ToInt32(row["RequestId"]),
                    QueueNumber = row["QueueNumber"].ToString(),
                    DocumentType = row["DocumentType"].ToString(),
                    Purpose = row["Purpose"].ToString(),
                    RequestDate = Convert.ToDateTime(row["RequestDate"]),
                    Status = row["Status"].ToString()
                });
            }
        }

        private async Task LoadAllRequests(int userId)
        {
            var query = @"SELECT RequestId, QueueNumber, DocumentType, Purpose, Quantity, RequestDate, Status 
                         FROM DocumentRequests 
                         WHERE UserId = @UserId AND Status NOT IN ('Completed', 'Cancelled')
                         ORDER BY RequestDate DESC";

            var parameters = new Dictionary<string, object> { { "@UserId", userId } };
            var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

            foreach (DataRow row in result.Rows)
            {
                AllRequests.Add(new DocumentRequest
                {
                    RequestId = Convert.ToInt32(row["RequestId"]),
                    QueueNumber = row["QueueNumber"].ToString(),
                    DocumentType = row["DocumentType"].ToString(),
                    Purpose = row["Purpose"].ToString(),
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    RequestDate = Convert.ToDateTime(row["RequestDate"]),
                    Status = row["Status"].ToString()
                });
            }
        }

        // Load history (completed and cancelled)
        public async Task<List<DocumentRequest>> LoadHistoryAsync(int userId)
        {
            var query = @"SELECT RequestId, QueueNumber, DocumentType, Purpose, Quantity, 
                                RequestDate, Status, CompletedDate
                         FROM DocumentRequests 
                         WHERE UserId = @UserId AND Status IN ('Completed', 'Cancelled')
                         ORDER BY 
                            CASE WHEN CompletedDate IS NOT NULL THEN CompletedDate ELSE RequestDate END DESC";

            var parameters = new Dictionary<string, object> { { "@UserId", userId } };
            var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

            var history = new List<DocumentRequest>();
            foreach (DataRow row in result.Rows)
            {
                history.Add(new DocumentRequest
                {
                    RequestId = Convert.ToInt32(row["RequestId"]),
                    QueueNumber = row["QueueNumber"].ToString(),
                    DocumentType = row["DocumentType"].ToString(),
                    Purpose = row["Purpose"].ToString(),
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    RequestDate = Convert.ToDateTime(row["RequestDate"]),
                    Status = row["Status"].ToString(),
                    CompletedDate = row["CompletedDate"] != DBNull.Value ? Convert.ToDateTime(row["CompletedDate"]) : (DateTime?)null
                });
            }
            return history;
        }
    }

    public class DocumentRequest
    {
        public int RequestId { get; set; }
        public string QueueNumber { get; set; }
        public string DocumentType { get; set; }
        public string Purpose { get; set; }
        public int Quantity { get; set; }
        public DateTime RequestDate { get; set; }
        public string Status { get; set; }
        public DateTime? CompletedDate { get; set; }
    }
}