using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using ProjectCapstone.Services;

namespace ProjectCapstone.Pages.DocumentRequest
{
    public class RequestModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<RequestModel> _logger;

        [BindProperty]
        public int DocumentTypeId { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        [BindProperty]
        public string Purpose { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        public List<Models.DocumentType> DocumentTypes { get; set; } = new();

        public RequestModel(MongoDBService mongoDBService, ILogger<RequestModel> logger)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadDocumentTypes();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            if (!ModelState.IsValid)
            {
                await LoadDocumentTypes();
                return Page();
            }

            try
            {
                // Get document type details
                var docType = await _mongoDBService.GetDocumentTypeByIdAsync(DocumentTypeId);

                if (docType == null)
                {
                    ErrorMessage = "Invalid document type selected.";
                    await LoadDocumentTypes();
                    return Page();
                }

                // Calculate total and dates
                decimal totalAmount = docType.Amount * Quantity;
                var targetReleaseDate = DateTime.UtcNow.AddDays(docType.ProcessingDays);

                // Generate queue number
                string queueNumber = $"CDM-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";

                // Determine initial stage and payment status
                string currentStage = docType.RequiresPayment ? "Pending Payment" : "Pending Review";
                string paymentStatus = docType.RequiresPayment ? "Pending" : "Not Required";

                // Create document request
                var newRequest = new Models.DocumentRequest
                {
                    UserId = userId.Value,
                    DocumentTypeId = DocumentTypeId,
                    DocumentType = docType.DocumentName,
                    Purpose = SecurityHelper.SanitizeInput(Purpose),
                    Quantity = Quantity,
                    TotalAmount = totalAmount,
                    PaymentStatus = paymentStatus,
                    CurrentStage = currentStage,
                    Status = "Active",
                    QueueNumber = queueNumber,
                    RequestDate = DateTime.UtcNow,
                    TargetReleaseDate = targetReleaseDate
                };

                var createdRequest = await _mongoDBService.CreateRequestAsync(newRequest);

                // Create workflow history entry
                await _mongoDBService.CreateWorkflowHistoryAsync(new WorkflowHistory
                {
                    RequestId = createdRequest.RequestId,
                    Stage = currentStage,
                    Action = "Request Submitted",
                    Comments = $"Document requested: {docType.DocumentName}, Quantity: {Quantity}",
                    ProcessedBy = userId.Value
                });

                _logger.LogInformation($"Document request created: RequestId={createdRequest.RequestId}, UserId={userId}");

                // Redirect based on payment requirement
                if (docType.RequiresPayment)
                {
                    return RedirectToPage("/DocumentRequest/Payment", new { requestId = createdRequest.RequestId });
                }
                else
                {
                    SuccessMessage = $"Request submitted successfully! Queue Number: {queueNumber}";
                    return RedirectToPage("/Dashboard/Student");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating document request: {ex.Message}");
                ErrorMessage = "An error occurred while processing your request. Please try again.";
                await LoadDocumentTypes();
                return Page();
            }
        }

        private async Task LoadDocumentTypes()
        {
            try
            {
                DocumentTypes = await _mongoDBService.GetActiveDocumentTypesAsync();

                if (DocumentTypes.Count == 0)
                {
                    _logger.LogWarning("No active document types found in database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading document types: {ex.Message}");
                DocumentTypes = new List<Models.DocumentType>();
            }
        }
    }
}
