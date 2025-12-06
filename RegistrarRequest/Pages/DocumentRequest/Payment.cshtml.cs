using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProjectCapstone.Models;
using ProjectCapstone.Services;

namespace ProjectCapstone.Pages.DocumentRequest
{
    [IgnoreAntiforgeryToken]
    public class PaymentModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PaymentModel> _logger;

        public Models.DocumentRequest? DocumentRequest { get; set; }
        public Payment? ExistingPayment { get; set; }

        public PaymentModel(MongoDBService mongoDBService, IWebHostEnvironment environment, ILogger<PaymentModel> logger)
        {
            _mongoDBService = mongoDBService;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(int requestId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            DocumentRequest = await _mongoDBService.GetRequestByIdAsync(requestId);
            if (DocumentRequest == null || DocumentRequest.UserId != userId.Value)
            {
                return RedirectToPage("/Dashboard/Student");
            }

            ExistingPayment = await _mongoDBService.GetPaymentByRequestIdAsync(requestId);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Get values directly from Request object
            int requestId = int.Parse(Request.Form["requestId"]);
            string paymentMethod = Request.Form["PaymentMethod"];
            string? referenceNumber = Request.Form["ReferenceNumber"];
            IFormFile? proofImage = Request.Form.Files["ProofImage"];

            _logger.LogInformation($"Payment upload - RequestId: {requestId}, Method: {paymentMethod}, File: {proofImage?.FileName}");

            DocumentRequest = await _mongoDBService.GetRequestByIdAsync(requestId);
            if (DocumentRequest == null || DocumentRequest.UserId != userId.Value)
            {
                TempData["ErrorMessage"] = "Request not found or access denied.";
                return RedirectToPage("/Dashboard/Student");
            }

            try
            {
                if (proofImage == null || proofImage.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please upload a payment proof image.";
                    return Page();
                }

                _logger.LogInformation($"File received: {proofImage.FileName}, Size: {proofImage.Length} bytes");

                string imagePath = await SavePaymentProof(proofImage, requestId);

                _logger.LogInformation($"Image saved to: {imagePath}");

                var payment = new Payment
                {
                    RequestId = requestId,
                    Amount = DocumentRequest.TotalAmount,
                    PaymentMethod = paymentMethod,
                    ReferenceNumber = string.IsNullOrEmpty(referenceNumber) ? null : referenceNumber,
                    PaymentProofUrl = imagePath,
                    PaymentDate = DateTime.UtcNow,
                    Status = "Pending Verification"

                };

                await _mongoDBService.CreatePaymentAsync(payment);
                _logger.LogInformation($"Payment record created with PaymentId: {payment.PaymentId}");

                await _mongoDBService.UpdateRequestStageAsync(
                    requestId,
                    "Payment Verification",
                    "Pending Verification"
                );

                _logger.LogInformation($"Request {requestId} updated to Pending Verification");

                TempData["SuccessMessage"] = "Payment proof uploaded successfully! Waiting for admin verification.";
                return RedirectToPage("/Dashboard/Student");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading payment: {ex.Message}\n{ex.StackTrace}");
                TempData["ErrorMessage"] = $"Failed to upload payment: {ex.Message}";
                return Page();
            }
        }

        private async Task<string> SavePaymentProof(IFormFile file, int requestId)
        {
            if (file == null || file.Length == 0)
            {
                throw new Exception("No file uploaded");
            }

            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "payments");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"payment_{requestId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/payments/{fileName}";
        }
    }
}
