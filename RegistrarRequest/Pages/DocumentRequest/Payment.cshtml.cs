using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Models;
using ProjectCapstone.Services;

namespace ProjectCapstone.Pages.DocumentRequest
{
    public class PaymentModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<PaymentModel> _logger;
        private readonly IWebHostEnvironment _environment;

        [BindProperty(SupportsGet = true)]
        public int RequestId { get; set; }

        [BindProperty]
        public string PaymentMethod { get; set; } = string.Empty;

        [BindProperty]
        public string ReferenceNumber { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? PaymentProofFile { get; set; }

        [BindProperty]
        public string Notes { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        public Models.DocumentRequest? Request { get; set; }
        public Payment? ExistingPayment { get; set; }

        public PaymentModel(MongoDBService mongoDBService, ILogger<PaymentModel> logger, IWebHostEnvironment environment)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
            _environment = environment;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadRequestDetails(userId.Value);

            if (Request == null)
            {
                ErrorMessage = "Request not found or you don't have permission to view it.";
                return Page();
            }

            await LoadExistingPayment();

            if (ExistingPayment != null)
            {
                PaymentMethod = ExistingPayment.PaymentMethod ?? string.Empty;
                ReferenceNumber = ExistingPayment.ReferenceNumber ?? string.Empty;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadRequestDetails(userId.Value);
            if (Request == null)
            {
                ErrorMessage = "Request not found or you don't have permission to view it.";
                return Page();
            }

            await LoadExistingPayment();

            if (!ModelState.IsValid || PaymentProofFile == null)
            {
                ErrorMessage = "Please fill in all required fields and upload payment proof.";
                await LoadRequestDetails(userId.Value);
                return Page();
            }

            try
            {
                // Validate file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var fileExtension = Path.GetExtension(PaymentProofFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ErrorMessage = "Invalid file format. Only JPG, PNG, and PDF files are allowed.";
                    await LoadRequestDetails(userId.Value);
                    return Page();
                }

                if (PaymentProofFile.Length > 5 * 1024 * 1024)
                {
                    ErrorMessage = "File size must be less than 5MB.";
                    await LoadRequestDetails(userId.Value);
                    return Page();
                }

                // Create uploads directory
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Generate unique filename
                var fileName = $"{RequestId}_{DateTime.UtcNow:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsPath, fileName);
                var relativeFilePath = $"/uploads/receipts/{fileName}";

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PaymentProofFile.CopyToAsync(stream);
                }

                if (ExistingPayment != null)
                {
                    if (string.Equals(ExistingPayment.Status, "Verified", StringComparison.OrdinalIgnoreCase))
                    {
                        ErrorMessage = "Payment already verified. No further uploads allowed.";
                        return RedirectToPage("/Dashboard/Student");
                    }

                    // Update existing payment
                    ExistingPayment.Amount = Request.TotalAmount;
                    ExistingPayment.PaymentMethod = PaymentMethod;
                    ExistingPayment.ReferenceNumber = ReferenceNumber;
                    ExistingPayment.PaymentProofUrl = relativeFilePath;
                    ExistingPayment.Status = "Pending";
                    ExistingPayment.PaymentDate = DateTime.UtcNow;
                    ExistingPayment.RejectionReason = null;
                    ExistingPayment.UpdatedDate = DateTime.UtcNow;

                    await _mongoDBService.UpdatePaymentAsync(ExistingPayment.PaymentId, ExistingPayment);
                }
                else
                {
                    // Create new payment
                    var newPayment = new Payment
                    {
                        RequestId = RequestId,
                        Amount = Request.TotalAmount,
                        PaymentMethod = PaymentMethod,
                        ReferenceNumber = ReferenceNumber,
                        PaymentProofUrl = relativeFilePath,
                        Status = "Pending",
                        PaymentDate = DateTime.UtcNow
                    };

                    await _mongoDBService.CreatePaymentAsync(newPayment);
                }

                // Update request status
                await _mongoDBService.UpdateRequestStageAsync(
                    RequestId,
                    "Payment Verification",
                    "Pending Verification"
                );

                // Create workflow history
                await _mongoDBService.CreateWorkflowHistoryAsync(new WorkflowHistory
                {
                    RequestId = RequestId,
                    Stage = "Payment Verification",
                    Action = "Payment Proof Uploaded",
                    Comments = $"Payment Method: {PaymentMethod}, Ref: {ReferenceNumber}. {Notes}",
                    ProcessedBy = userId.Value
                });

                _logger.LogInformation($"Payment uploaded for RequestId={RequestId} by UserId={userId}");

                SuccessMessage = "Payment proof uploaded successfully! Waiting for verification from Accounting Office.";
                return RedirectToPage("/Dashboard/Student");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading payment: {ex.Message}");
                ErrorMessage = "An error occurred while uploading payment proof. Please try again.";
                await LoadRequestDetails(userId.Value);
                return Page();
            }
        }

        private async Task LoadRequestDetails(int userId)
        {
            try
            {
                var request = await _mongoDBService.GetRequestByIdAsync(RequestId);

                if (request != null && request.UserId == userId)
                {
                    Request = new Models.DocumentRequest
                    {
                        RequestId = request.RequestId,
                        QueueNumber = request.QueueNumber,
                        DocumentType = request.DocumentType,
                        Quantity = request.Quantity,
                        TotalAmount = request.TotalAmount,
                        CurrentStage = request.CurrentStage,
                        PaymentStatus = request.PaymentStatus
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading request details: {ex.Message}");
            }
        }

        private async Task LoadExistingPayment()
        {
            try
            {
                ExistingPayment = await _mongoDBService.GetPaymentByRequestIdAsync(RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading existing payment: {ex.Message}");
            }
        }
    }
}
