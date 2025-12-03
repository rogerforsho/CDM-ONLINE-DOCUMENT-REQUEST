using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Services;

namespace ProjectCapstone.Pages.Dashboard
{
    public class StudentModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<StudentModel> _logger;

        public string FullName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int ReadyCount { get; set; }
        public int TotalRequests { get; set; }
        public List<DocumentRequest> RecentRequests { get; set; }
        public List<DocumentRequest> AllRequests { get; set; }
        public List<DocumentRequest> HistoryRequests { get; set; }

        public StudentModel(MongoDBService mongoDBService, ILogger<StudentModel> logger)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
            RecentRequests = new List<DocumentRequest>();
            AllRequests = new List<DocumentRequest>();
            HistoryRequests = new List<DocumentRequest>();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            FullName = HttpContext.Session.GetString("FullName") ?? string.Empty;
            StudentNumber = HttpContext.Session.GetString("StudentNumber") ?? string.Empty;

            try
            {
                await LoadStatistics(userId.Value);
                await LoadRecentRequests(userId.Value);
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
            PendingCount = await _mongoDBService.GetCountByStatusAsync(userId, "Pending");
            ProcessingCount = await _mongoDBService.GetCountByStatusAsync(userId, "Processing");
            ReadyCount = await _mongoDBService.GetCountByStatusAsync(userId, "Ready");
            TotalRequests = await _mongoDBService.GetTotalCountAsync(userId);
        }

        private async Task LoadRecentRequests(int userId)
        {
            var requests = await _mongoDBService.GetRequestsByUserIdAsync(userId);

            RecentRequests = requests.Take(5).Select(r => new DocumentRequest
            {
                RequestId = r.RequestId,
                QueueNumber = r.QueueNumber,
                DocumentType = r.DocumentType,
                Purpose = r.Purpose,
                RequestDate = r.RequestDate,
                Status = r.Status,
                PaymentStatus = r.PaymentStatus,
                TotalAmount = r.TotalAmount
            }).ToList();
        }

        private async Task LoadAllRequests(int userId)
        {
            var requests = await _mongoDBService.GetRequestsByUserIdAsync(userId);

            AllRequests = requests
                .Where(r => r.Status != "Completed" && r.Status != "Cancelled")
                .Select(r => new DocumentRequest
                {
                    RequestId = r.RequestId,
                    QueueNumber = r.QueueNumber,
                    DocumentType = r.DocumentType,
                    Purpose = r.Purpose,
                    Quantity = r.Quantity,
                    RequestDate = r.RequestDate,
                    Status = r.Status,
                    PaymentStatus = r.PaymentStatus,
                    TotalAmount = r.TotalAmount
                }).ToList();
        }

        public async Task<List<DocumentRequest>> LoadHistoryAsync(int userId)
        {
            var history = await _mongoDBService.GetHistoryAsync(userId);

            return history.Select(r => new DocumentRequest
            {
                RequestId = r.RequestId,
                QueueNumber = r.QueueNumber,
                DocumentType = r.DocumentType,
                Purpose = r.Purpose,
                Quantity = r.Quantity,
                RequestDate = r.RequestDate,
                Status = r.Status,
                CompletedDate = r.CompletedDate
            }).ToList();
        }

        public class DocumentRequest
        {
            public int RequestId { get; set; }
            public string QueueNumber { get; set; } = string.Empty;
            public string DocumentType { get; set; } = string.Empty;
            public string Purpose { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public DateTime RequestDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime? CompletedDate { get; set; }
            public string PaymentStatus { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
        }
    }
}
