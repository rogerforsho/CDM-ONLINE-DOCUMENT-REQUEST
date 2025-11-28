using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Services;
using System.Data;

namespace ProjectCapstone.Pages.Dashboard
{
    public class AdminModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<AdminModel> _logger;
        private readonly IEmailService _emailService;

        public string FullName { get; set; }
        public string Email { get; set; }
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ReadyCount { get; set; }
        public int TotalRequests { get; set; }
        public List<AdminDocumentRequest> AllRequests { get; set; }

        // Add these two properties for the new Admin.cshtml
        public List<AdminDocumentRequest> PendingDocuments { get; set; }
        public List<AdminDocumentRequest> ReadyDocuments { get; set; }
        public List<AdminDocumentRequest> HistoryDocuments { get; set; }

        public Dictionary<string, int> DocumentTypeStats { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public AdminModel(IConfiguration configuration, ILogger<AdminModel> logger, IEmailService emailService)
        {
            _dbHelper = new DatabaseHelper(configuration);
            _logger = logger;
            _emailService = emailService;
            AllRequests = new List<AdminDocumentRequest>();
            PendingDocuments = new List<AdminDocumentRequest>();
            ReadyDocuments = new List<AdminDocumentRequest>();
            DocumentTypeStats = new Dictionary<string, int>();
        }

        public string GetDocumentTypesJson()
        {
            var types = DocumentTypeStats.Keys.Select(k => $"\"{k}\"");
            return "[" + string.Join(",", types) + "]";
        }

        public string GetDocumentCountsJson()
        {
            var counts = DocumentTypeStats.Values;
            return "[" + string.Join(",", counts) + "]";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Check if user is admin or staff
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "Staff")
            {
                return RedirectToPage("/Dashboard/Student");
            }

            // Get user info from session
            FullName = HttpContext.Session.GetString("FullName");
            Email = HttpContext.Session.GetString("Email");

            try
            {
                // Get statistics for all requests (admin sees everything)
                await LoadStatistics();

                // Load all document requests
                await LoadAllRequests();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Admin dashboard loading error: {ex.Message}");
            }

            return Page();
        }

        // Add this new handler for MarkReady
        public async Task<IActionResult> OnPostMarkReadyAsync(int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToPage("/Login");
                }

                // Get student email and document details BEFORE updating status
                var getDetailsQuery = @"SELECT u.Email, u.FirstName, u.LastName, 
                                              dr.DocumentType, dr.QueueNumber
                                       FROM DocumentRequests dr
                                       INNER JOIN Users u ON dr.UserId = u.UserId
                                       WHERE dr.RequestId = @RequestId";

                var detailsParams = new Dictionary<string, object> { { "@RequestId", requestId } };
                var detailsResult = await _dbHelper.ExecuteQueryAsync(getDetailsQuery, detailsParams);

                string studentEmail = null;
                string studentName = null;
                string documentType = null;
                string queueNumber = null;

                if (detailsResult.Rows.Count > 0)
                {
                    var row = detailsResult.Rows[0];
                    studentEmail = row["Email"].ToString();
                    studentName = $"{row["FirstName"]} {row["LastName"]}";
                    documentType = row["DocumentType"].ToString();
                    queueNumber = row["QueueNumber"].ToString();
                }

                // Update status to Ready
                var query = @"UPDATE DocumentRequests 
                             SET Status = @Status, 
                                 ProcessedBy = @ProcessedBy, 
                                 ProcessedDate = @ProcessedDate 
                             WHERE RequestId = @RequestId";

                var parameters = new Dictionary<string, object>
                {
                    { "@Status", "Ready" },
                    { "@ProcessedBy", userId.Value },
                    { "@ProcessedDate", DateTime.Now },
                    { "@RequestId", requestId }
                };

                await _dbHelper.ExecuteNonQueryAsync(query, parameters);

                _logger.LogInformation($"Request {requestId} status updated to Ready by admin {userId}");

                // Send email notification
                if (!string.IsNullOrEmpty(studentEmail))
                {
                    try
                    {
                        await _emailService.SendDocumentReadyEmailAsync(
                            studentEmail,
                            studentName,
                            documentType,
                            queueNumber
                        );

                        SuccessMessage = $"✅ Document marked as Ready and email sent to {studentName}!";
                        _logger.LogInformation($"📧 Email notification sent to {studentEmail}");
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError($"❌ Error sending email: {emailEx.Message}");
                        SuccessMessage = "✅ Document marked as Ready (email notification failed)";
                    }
                }
                else
                {
                    SuccessMessage = "✅ Document marked as Ready";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating status: {ex.Message}");
                ErrorMessage = $"❌ Error updating status: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Add this new handler for MarkCompleted
        public async Task<IActionResult> OnPostMarkCompletedAsync(int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToPage("/Login");
                }

                var query = @"UPDATE DocumentRequests 
                             SET Status = @Status, 
                                 ProcessedBy = @ProcessedBy, 
                                 ProcessedDate = @ProcessedDate,
                                 CompletedDate = @CompletedDate
                             WHERE RequestId = @RequestId";

                var parameters = new Dictionary<string, object>
                {
                    { "@Status", "Completed" },
                    { "@ProcessedBy", userId.Value },
                    { "@ProcessedDate", DateTime.Now },
                    { "@CompletedDate", DateTime.Now },
                    { "@RequestId", requestId }
                };

                await _dbHelper.ExecuteNonQueryAsync(query, parameters);

                SuccessMessage = "✅ Document marked as Completed!";
                _logger.LogInformation($"Request {requestId} marked as completed by admin {userId}");

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking completed: {ex.Message}");
                ErrorMessage = $"❌ Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int requestId, string newStatus)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToPage("/Login");
                }

                // Get student email and document details BEFORE updating status
                var getDetailsQuery = @"SELECT u.Email, u.FirstName, u.LastName, 
                                              dr.DocumentType, dr.QueueNumber
                                       FROM DocumentRequests dr
                                       INNER JOIN Users u ON dr.UserId = u.UserId
                                       WHERE dr.RequestId = @RequestId";

                var detailsParams = new Dictionary<string, object> { { "@RequestId", requestId } };
                var detailsResult = await _dbHelper.ExecuteQueryAsync(getDetailsQuery, detailsParams);

                string studentEmail = null;
                string studentName = null;
                string documentType = null;
                string queueNumber = null;

                if (detailsResult.Rows.Count > 0)
                {
                    var row = detailsResult.Rows[0];
                    studentEmail = row["Email"].ToString();
                    studentName = $"{row["FirstName"]} {row["LastName"]}";
                    documentType = row["DocumentType"].ToString();
                    queueNumber = row["QueueNumber"].ToString();
                }

                // Update status in database
                string query;
                Dictionary<string, object> parameters;

                // If marking as completed or cancelled, set CompletedDate
                if (newStatus == "Completed" || newStatus == "Cancelled")
                {
                    query = @"UPDATE DocumentRequests 
                             SET Status = @Status, 
                                 ProcessedBy = @ProcessedBy, 
                                 ProcessedDate = @ProcessedDate,
                                 CompletedDate = @CompletedDate
                             WHERE RequestId = @RequestId";

                    parameters = new Dictionary<string, object>
                    {
                        { "@Status", newStatus },
                        { "@ProcessedBy", userId.Value },
                        { "@ProcessedDate", DateTime.Now },
                        { "@CompletedDate", DateTime.Now },
                        { "@RequestId", requestId }
                    };
                }
                else
                {
                    query = @"UPDATE DocumentRequests 
                             SET Status = @Status, 
                                 ProcessedBy = @ProcessedBy, 
                                 ProcessedDate = @ProcessedDate 
                             WHERE RequestId = @RequestId";

                    parameters = new Dictionary<string, object>
                    {
                        { "@Status", newStatus },
                        { "@ProcessedBy", userId.Value },
                        { "@ProcessedDate", DateTime.Now },
                        { "@RequestId", requestId }
                    };
                }

                await _dbHelper.ExecuteNonQueryAsync(query, parameters);

                _logger.LogInformation($"Request {requestId} status updated to {newStatus} by admin {userId}");

                // Send email notification if status is Ready
                if (newStatus == "Ready" && !string.IsNullOrEmpty(studentEmail))
                {
                    try
                    {
                        await _emailService.SendDocumentReadyEmailAsync(
                            studentEmail,
                            studentName,
                            documentType,
                            queueNumber
                        );

                        SuccessMessage = $"✅ Document marked as Ready and email sent to {studentName}!";
                        _logger.LogInformation($"📧 Email notification sent to {studentEmail}");
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError($"❌ Error sending email: {emailEx.Message}");
                        SuccessMessage = "✅ Document marked as Ready (email notification failed)";
                    }
                }
                else
                {
                    SuccessMessage = $"✅ Document status updated to {newStatus}";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating status: {ex.Message}");
                ErrorMessage = $"❌ Error updating status: {ex.Message}";
                return RedirectToPage();
            }
        }

        private async Task LoadStatistics()
        {
            // Get pending count (all users)
            var pendingQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE Status = 'Pending'";
            PendingCount = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(pendingQuery));

            // Get processing count (all users)
            var processingQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE Status = 'Processing'";
            ProcessingCount = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(processingQuery));

            // Get ready count (all users)
            var readyQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE Status = 'Ready'";
            ReadyCount = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(readyQuery));

            // Get total requests (all users)
            var totalQuery = "SELECT COUNT(*) FROM DocumentRequests";
            TotalRequests = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(totalQuery));
        }

        private async Task LoadAllRequests()
        {
            var query = @"SELECT dr.RequestId, dr.QueueNumber, dr.DocumentType, dr.Purpose, 
                                dr.Quantity, dr.RequestDate, dr.Status,
                                u.FirstName, u.LastName, u.StudentNumber, u.Email
                         FROM DocumentRequests dr
                         INNER JOIN Users u ON dr.UserId = u.UserId
                         WHERE dr.Status IN ('Pending', 'Processing', 'Ready')
                         ORDER BY 
                            CASE 
                                WHEN dr.Status = 'Pending' THEN 1
                                WHEN dr.Status = 'Processing' THEN 2
                                WHEN dr.Status = 'Ready' THEN 3
                            END,
                            dr.RequestDate ASC";

            var result = await _dbHelper.ExecuteQueryAsync(query);

            foreach (DataRow row in result.Rows)
            {
                var doc = new AdminDocumentRequest
                {
                    RequestId = Convert.ToInt32(row["RequestId"]),
                    QueueNumber = row["QueueNumber"].ToString(),
                    DocumentType = row["DocumentType"].ToString(),
                    Purpose = row["Purpose"].ToString(),
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    RequestDate = Convert.ToDateTime(row["RequestDate"]),
                    Status = row["Status"].ToString(),
                    StudentName = $"{row["FirstName"]} {row["LastName"]}",
                    StudentNumber = row["StudentNumber"].ToString(),
                    StudentEmail = row["Email"].ToString()
                };

                // Add to AllRequests
                AllRequests.Add(doc);

                // Split into PendingDocuments and ReadyDocuments
                if (doc.Status == "Pending" || doc.Status == "Processing")
                {
                    PendingDocuments.Add(doc);
                }
                else if (doc.Status == "Ready")
                {
                    ReadyDocuments.Add(doc);
                }
            }

            // Load document type statistics for analytics
            LoadDocumentTypeStats();
        }

        private void LoadDocumentTypeStats()
        {
            // Group by document type and count
            var stats = AllRequests
                .GroupBy(r => r.DocumentType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            DocumentTypeStats.Clear();
            foreach (var stat in stats)
            {
                DocumentTypeStats[stat.Type] = stat.Count;
            }
        }
    }

    public class AdminDocumentRequest
    {
        public int RequestId { get; set; }
        public string QueueNumber { get; set; }
        public string DocumentType { get; set; }
        public string Purpose { get; set; }
        public int Quantity { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? ReadyDate { get; set; }
        public string Status { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string StudentEmail { get; set; }
    }
}