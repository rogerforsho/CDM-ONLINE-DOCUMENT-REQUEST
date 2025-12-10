using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;
using ProjectCapstone.Models;
using ProjectCapstone.Services;
using System.Text;

namespace ProjectCapstone.Pages.Dashboard
{
    public class AdminModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<AdminModel> _logger;
        private readonly IEmailService _emailService;
        private const string ADMIN_PIN = "1234";  // ← Change to your PIN


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
        public List<Payment> AllPayments { get; set; }

        // ADDED: Property for pending payments
        public List<PaymentVerificationItem> PendingPayments { get; set; }

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
            // ADDED: Initialize PendingPayments
            PendingPayments = new List<PaymentVerificationItem>();
            AllPayments = new List<Payment>();
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
            var pinVerified = HttpContext.Session.GetString("PinVerified");
            if (pinVerified != "true")
            {
                return RedirectToPage("../AdminPin");
            }

            FullName = HttpContext.Session.GetString("FullName") ?? string.Empty;
            Email = HttpContext.Session.GetString("Email") ?? string.Empty;

            try
            {
                await LoadStatistics();
                await LoadAllRequests();
                await LoadHistoryDocuments();
                // ADDED: Load pending payments
                await LoadPendingPayments();
                await LoadAllPayments();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Admin dashboard loading error: {ex.Message}");
            }

            return Page();
        }

        private async Task LoadAllPayments()
        {
            AllPayments = await _mongoDBService.GetAllPaymentsAsync();
        }

        // CORRECT CSV EXPORT METHOD - KEEP THIS ONE
        public async Task<IActionResult> OnPostExportRecordsCSVAsync(string status, string documentType, string searchQuery)
        {
            try
            {
                // Get all requests with user info
                var requests = await _mongoDBService.GetAllRequestsWithUsersAsync();

                // Convert to AdminDocumentRequest
                var allRecords = requests.Select(r => new AdminDocumentRequest
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
                    StudentEmail = r.StudentEmail,
                    TotalAmount = r.TotalAmount,
                    // ✅ ADDED: Map department fields
                    DepartmentId = r.DepartmentId,
                    DepartmentCode = r.DepartmentCode ?? "N/A",
                    DepartmentName = r.DepartmentName ?? "Unknown"
                }).ToList();

                // Apply filters
                var filteredRecords = allRecords.AsEnumerable();

                if (status != "all")
                {
                    filteredRecords = filteredRecords.Where(r => r.Status == status);
                }

                if (documentType != "all")
                {
                    filteredRecords = filteredRecords.Where(r => r.DocumentType == documentType);
                }

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    searchQuery = searchQuery.ToLower();
                    filteredRecords = filteredRecords.Where(r =>
                        r.StudentName.ToLower().Contains(searchQuery) ||
                        r.StudentNumber.ToLower().Contains(searchQuery));
                }

                var recordsList = filteredRecords.OrderByDescending(r => r.RequestDate).ToList();

                // Build CSV
                var csv = new StringBuilder();
                csv.AppendLine("Queue Number,Student Name,Student Number,Email,Department,Document Type,Purpose,Quantity,Request Date,Status,Completed Date");

                foreach (var rec in recordsList)
                {
                    csv.AppendLine($"{rec.QueueNumber}," +
                                  $"\"{rec.StudentName}\"," +
                                  $"{rec.StudentNumber}," +
                                  $"\"{rec.StudentEmail}\"," +
                                  $"{rec.DepartmentCode}," + // ✅ ADDED
                                  $"\"{rec.DocumentType}\"," +
                                  $"\"{rec.Purpose}\"," +
                                  $"{rec.Quantity}," +
                                  $"{rec.RequestDate:yyyy-MM-dd HH:mm}," +
                                  $"{rec.Status}," +
                                  $"{(rec.ReadyDate.HasValue ? rec.ReadyDate.Value.ToString("yyyy-MM-dd HH:mm") : "-")}");
                }

                // Generate filename
                var statusPart = status != "all" ? status : "All";
                var docTypePart = documentType != "all" ? documentType.Replace(" ", "_") : "AllDocs";
                var fileName = $"CDM_Records_{statusPart}_{docTypePart}_{DateTime.Now:yyyy-MM-dd_HHmm}.csv";

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting CSV: {ex.Message}");
                ErrorMessage = $"❌ Error exporting CSV: {ex.Message}";
                return RedirectToPage();
            }
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
                    StudentEmail = r.StudentEmail,
                    TotalAmount = r.TotalAmount,
                    // ✅ ADDED: Map department fields
                    DepartmentId = r.DepartmentId,
                    DepartmentCode = r.DepartmentCode ?? "N/A",
                    DepartmentName = r.DepartmentName ?? "Unknown"
                }).ToList();

            PendingDocuments = AllRequests.Where(r => r.Status == "Pending" || r.Status == "Processing").ToList();
            ReadyDocuments = AllRequests.Where(r => r.Status == "Ready").ToList();

            LoadDocumentTypeStats();
        }

        private async Task LoadHistoryDocuments()
        {
            var requests = await _mongoDBService.GetAllRequestsWithUsersAsync();

            // CHANGED: Load ALL requests (not just Completed/Cancelled)
            HistoryDocuments = requests
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
                    StudentEmail = r.StudentEmail,
                    TotalAmount = r.TotalAmount,
                    // ✅ ADDED: Map department fields
                    DepartmentId = r.DepartmentId,
                    DepartmentCode = r.DepartmentCode ?? "N/A",
                    DepartmentName = r.DepartmentName ?? "Unknown"
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

        // ADDED: Load pending payments method
        private async Task LoadPendingPayments()
        {
            var payments = await _mongoDBService.GetPendingPaymentsAsync();

            PendingPayments = new List<PaymentVerificationItem>();

            foreach (var payment in payments)
            {
                var request = await _mongoDBService.GetRequestByIdAsync(payment.RequestId);
                var user = request != null ? await _mongoDBService.GetUserByIdAsync(request.UserId) : null;

                if (request != null && user != null)
                {
                    PendingPayments.Add(new PaymentVerificationItem
                    {
                        PaymentId = payment.PaymentId,
                        RequestId = payment.RequestId,
                        QueueNumber = request.QueueNumber,
                        StudentName = $"{user.FirstName} {user.LastName}",
                        StudentNumber = user.StudentNumber,
                        DocumentType = request.DocumentType,
                        Amount = request.TotalAmount,
                        PaymentMethod = payment.PaymentMethod,
                        ReferenceNumber = payment.ReferenceNumber,
                        PaymentProofUrl = payment.PaymentProofUrl,
                        PaymentDate = payment.PaymentDate
                    });
                }
            }
        }

        // ADDED: Approve payment handler
        public async Task<IActionResult> OnPostApprovePaymentAsync(int paymentId, int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null) return RedirectToPage("/Login");

                // Get request and user details
                var request = await _mongoDBService.GetRequestByIdAsync(requestId);
                var user = request != null ? await _mongoDBService.GetUserByIdAsync(request.UserId) : null;

                // Verify payment
                await _mongoDBService.VerifyPaymentAsync(paymentId, "Verified");

                // Send email notification
                if (request != null && user != null)
                {
                    try
                    {
                        await _emailService.SendPaymentVerificationEmailAsync(
                            user.Email,
                            $"{user.FirstName} {user.LastName}",
                            request.QueueNumber,
                            approved: true
                        );

                        SuccessMessage = $"✅ Payment verified! Email sent to {user.FirstName} {user.LastName}. You can now start processing the request.";
                        _logger.LogInformation($"Payment {paymentId} verified by admin {userId}");
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError($"Error sending approval email: {emailEx.Message}");
                        SuccessMessage = "✅ Payment verified! (Email notification failed)";
                    }
                }
                else
                {
                    SuccessMessage = "✅ Payment verified! You can now start processing the request.";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying payment: {ex.Message}");
                ErrorMessage = $"❌ Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        // ADDED: Reject payment handler
        public async Task<IActionResult> OnPostRejectPaymentAsync(int paymentId, string rejectionReason)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null) return RedirectToPage("/Login");

                // Get payment and request details
                var payment = await _mongoDBService.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    ErrorMessage = "Payment not found";
                    return RedirectToPage();
                }

                var request = await _mongoDBService.GetRequestByIdAsync(payment.RequestId);
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

                // Reject payment
                await _mongoDBService.VerifyPaymentAsync(paymentId, "Rejected", rejectionReason);

                // Send email notification
                try
                {
                    await _emailService.SendPaymentVerificationEmailAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        request.QueueNumber,
                        approved: false,
                        reason: rejectionReason
                    );

                    SuccessMessage = $"❌ Payment rejected and email sent to {user.FirstName} {user.LastName}!";
                    _logger.LogInformation($"Payment {paymentId} rejected and email sent to {user.Email}");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError($"Error sending rejection email: {emailEx.Message}");
                    SuccessMessage = "❌ Payment rejected (email notification failed)";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error rejecting payment: {ex.Message}");
                ErrorMessage = $"❌ Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostStartProcessingAsync(int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToPage("/Login");
                }

                // Get request details
                var request = await _mongoDBService.GetRequestByIdAsync(requestId);
                if (request == null)
                {
                    ErrorMessage = "Request not found";
                    return RedirectToPage();
                }

                // Update status to Processing
                await _mongoDBService.UpdateRequestStatusAsync(requestId, "Processing", userId.Value);

                SuccessMessage = $"✅ Request #{request.QueueNumber} moved to Processing!";
                _logger.LogInformation($"Request {requestId} status updated to Processing by admin {userId}");

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting processing: {ex.Message}");
                ErrorMessage = $"❌ Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostCompleteRequestAsync(int requestId)
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

                // Update status to Completed
                await _mongoDBService.UpdateRequestStatusAsync(requestId, "Completed", userId.Value, DateTime.UtcNow);

                SuccessMessage = $"✅ Request #{request.QueueNumber} marked as Completed!";
                _logger.LogInformation($"Request {requestId} completed by admin {userId}");

                // Optional: Send completion email
                if (user != null)
                {
                    try
                    {
                        await _emailService.SendDocumentReadyEmailAsync(
                            user.Email,
                            $"{user.FirstName} {user.LastName}",
                            request.DocumentType,
                            request.QueueNumber
                        );
                        _logger.LogInformation($"Completion email sent to {user.Email}");
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError($"Error sending completion email: {emailEx.Message}");
                    }
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error completing request: {ex.Message}");
                ErrorMessage = $"❌ Error: {ex.Message}";
                return RedirectToPage();
            }
        }

    } // ← AdminModel class ends here

    // ✅ UPDATED: AdminDocumentRequest with department fields
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
        public decimal TotalAmount { get; set; }

        // ✅ ADDED: Department properties
        public int? DepartmentId { get; set; }
        public string DepartmentCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    // ADDED: Payment verification helper class
    public class PaymentVerificationItem
    {
        public int PaymentId { get; set; }
        public int RequestId { get; set; }
        public string QueueNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string PaymentProofUrl { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
    }
}
