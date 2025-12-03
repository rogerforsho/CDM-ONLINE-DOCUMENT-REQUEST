using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Services;
using MongoDB.Driver;

namespace ProjectCapstone.Pages.Dashboard
{
    public class AdminModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<AdminModel> _logger;
        private readonly IEmailService _emailService;

        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ReadyCount { get; set; }
        public int TotalRequests { get; set; }
        public List<AdminDocumentRequest> AllRequests { get; set; }
        public List<AdminDocumentRequest> PendingDocuments { get; set; }
        public List<AdminDocumentRequest> ReadyDocuments { get; set; }
        public List<AdminDocumentRequest> HistoryDocuments { get; set; }
        public Dictionary<string, int> DocumentTypeStats { get; set; }

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        public AdminModel(MongoDBService mongoDBService, ILogger<AdminModel> logger, IEmailService emailService)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
            _emailService = emailService;
            AllRequests = new List<AdminDocumentRequest>();
            PendingDocuments = new List<AdminDocumentRequest>();
            ReadyDocuments = new List<AdminDocumentRequest>();
            HistoryDocuments = new List<AdminDocumentRequest>();
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
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "Staff")
            {
                return RedirectToPage("/Dashboard/Student");
            }

            FullName = HttpContext.Session.GetString("FullName") ?? string.Empty;
            Email = HttpContext.Session.GetString("Email") ?? string.Empty;

            try
            {
                await LoadStatistics();
                await LoadAllRequests();
                await LoadHistoryDocuments();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Admin dashboard loading error: {ex.Message}");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostMarkReadyAsync(int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToPage("/Login");
                }

                // Get request and user details
                var request = await _mongoDBService.GetRequestByIdAsync(requestId);
                if (request == null)
                {
                    ErrorMessage = "Request not found";
                    return RedirectToPage();
                }

                var user = await _mongoDBService.GetUserByIdAsync(request.UserId);
                if (user == null)
                {
                    ErrorMessage = "User not found";
                    return RedirectToPage();
                }

                // Update status
                await _mongoDBService.UpdateRequestStatusAsync(requestId, "Ready", userId.Value);

                _logger.LogInformation($"Request {requestId} status updated to Ready by admin {userId}");

                // Send email notification
                try
                {
                    await _emailService.SendDocumentReadyEmailAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        request.DocumentType,
                        request.QueueNumber
                    );

                    SuccessMessage = $"✅ Document marked as Ready and email sent to {user.FirstName} {user.LastName}!";
                    _logger.LogInformation($"📧 Email notification sent to {user.Email}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError($"❌ Error sending email: {emailEx.Message}");
                    SuccessMessage = "✅ Document marked as Ready (email notification failed)";
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

        public async Task<IActionResult> OnPostMarkCompletedAsync(int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToPage("/Login");
                }

                await _mongoDBService.UpdateRequestStatusAsync(requestId, "Completed", userId.Value, DateTime.UtcNow);

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

                // Get request and user details
                var request = await _mongoDBService.GetRequestByIdAsync(requestId);
                if (request == null)
                {
                    ErrorMessage = "Request not found";
                    return RedirectToPage();
                }

                var user = await _mongoDBService.GetUserByIdAsync(request.UserId);

                // Update status
                DateTime? completedDate = (newStatus == "Completed" || newStatus == "Cancelled") ? DateTime.UtcNow : null;
                await _mongoDBService.UpdateRequestStatusAsync(requestId, newStatus, userId.Value, completedDate);

                _logger.LogInformation($"Request {requestId} status updated to {newStatus} by admin {userId}");

                // Send email if Ready
                if (newStatus == "Ready" && user != null)
                {
                    try
                    {
                        await _emailService.SendDocumentReadyEmailAsync(
                            user.Email,
                            $"{user.FirstName} {user.LastName}",
                            request.DocumentType,
                            request.QueueNumber
                        );

                        SuccessMessage = $"✅ Document marked as Ready and email sent to {user.FirstName} {user.LastName}!";
                        _logger.LogInformation($"📧 Email notification sent to {user.Email}");
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
            var allRequests = await _mongoDBService.GetAllRequestsAsync();

            PendingCount = allRequests.Count(r => r.Status == "Pending");
            ProcessingCount = allRequests.Count(r => r.Status == "Processing");
            ReadyCount = allRequests.Count(r => r.Status == "Ready");
            TotalRequests = allRequests.Count;
        }

        private async Task LoadAllRequests()
        {
            var requests = await _mongoDBService.GetAllRequestsWithUsersAsync();

            AllRequests = requests
                .Where(r => r.Status == "Pending" || r.Status == "Processing" || r.Status == "Ready")
                .OrderBy(r => r.Status == "Pending" ? 1 : r.Status == "Processing" ? 2 : 3)
                .ThenBy(r => r.RequestDate)
                .Select(r => new AdminDocumentRequest
                {
                    RequestId = r.RequestId,
                    QueueNumber = r.QueueNumber,
                    DocumentType = r.DocumentType,
                    Purpose = r.Purpose,
                    Quantity = r.Quantity,
                    RequestDate = r.RequestDate,
                    Status = r.Status,
                    StudentName = r.StudentName,
                    StudentNumber = r.StudentNumber,
                    StudentEmail = r.StudentEmail
                }).ToList();

            PendingDocuments = AllRequests.Where(r => r.Status == "Pending" || r.Status == "Processing").ToList();
            ReadyDocuments = AllRequests.Where(r => r.Status == "Ready").ToList();

            LoadDocumentTypeStats();
        }

        private async Task LoadHistoryDocuments()
        {
            var requests = await _mongoDBService.GetAllRequestsWithUsersAsync();

            HistoryDocuments = requests
                .Where(r => r.Status == "Completed" || r.Status == "Cancelled")
                .OrderByDescending(r => r.CompletedDate ?? r.RequestDate)
                .Select(r => new AdminDocumentRequest
                {
                    RequestId = r.RequestId,
                    QueueNumber = r.QueueNumber,
                    DocumentType = r.DocumentType,
                    Purpose = r.Purpose,
                    Quantity = r.Quantity,
                    RequestDate = r.RequestDate,
                    ReadyDate = r.CompletedDate,
                    Status = r.Status,
                    StudentName = r.StudentName,
                    StudentNumber = r.StudentNumber,
                    StudentEmail = r.StudentEmail
                }).ToList();
        }

        private void LoadDocumentTypeStats()
        {
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
        public string QueueNumber { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? ReadyDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
    }
}
