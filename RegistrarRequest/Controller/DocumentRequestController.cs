using Microsoft.AspNetCore.Mvc;
using ProjectCapstone.Services;
using ProjectCapstone.Models;
using ProjectCapstone.Helpers;

namespace ProjectCapstone.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentRequestController : ControllerBase
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<DocumentRequestController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DocumentRequestController(
            MongoDBService mongoDBService,
            ILogger<DocumentRequestController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }
        [HttpPost("Submit")]
        public async Task<IActionResult> SubmitRequest([FromBody] DocumentRequestDto request)
        {
            try
            {
                _logger.LogInformation("📝 Submit request received");

                var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");

                _logger.LogInformation($"🔐 UserId from session: {userId}");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in - no session UserId");
                    return Unauthorized(new { success = false, message = "User not logged in" });
                }

                // ADDED: Look up the document type to get pricing
                var documentType = await _mongoDBService.GetDocumentTypeByIdAsync(request.DocumentTypeId);
                if (documentType == null)
                {
                    _logger.LogWarning($"⚠️ Document type not found: {request.DocumentTypeId}");
                    return BadRequest(new { success = false, message = "Invalid document type" });
                }

                var queueNumber = SecurityHelper.GenerateQueueNumber();
                _logger.LogInformation($"🎫 Generated queue number: {queueNumber}");

                // ADDED: Calculate total amount based on document price and quantity
                decimal totalAmount = documentType.Amount * request.Quantity;

                // ADDED: Determine payment status
                string paymentStatus = documentType.RequiresPayment ? "Pending Payment" : "Not Required";
                string currentStage = documentType.RequiresPayment ? "Awaiting Payment" : "Pending Review";

                var documentRequest = new DocumentRequest
                {
                    UserId = userId.Value,
                    DocumentTypeId = request.DocumentTypeId, // ADDED
                    DocumentType = SecurityHelper.SanitizeInput(request.DocumentType ?? documentType.DocumentName), // Use name from DB if not provided
                    Purpose = SecurityHelper.SanitizeInput(request.Purpose ?? string.Empty),
                    Quantity = request.Quantity,
                    TotalAmount = totalAmount, // ADDED
                    PaymentStatus = paymentStatus, // ADDED
                    CurrentStage = currentStage, // ADDED
                    RequestDate = DateTime.UtcNow,
                    Status = "Active", // CHANGED from "Pending" to "Active"
                    QueueNumber = queueNumber,
                    Notes = SecurityHelper.SanitizeInput(request.Notes ?? string.Empty)
                };

                await _mongoDBService.CreateRequestAsync(documentRequest);

                _logger.LogInformation($"✅ Document request submitted. Queue#: {queueNumber}, UserId: {userId}, Amount: ₱{totalAmount}");

                return Ok(new
                {
                    success = true,
                    queueNumber = queueNumber,
                    totalAmount = totalAmount,
                    requiresPayment = documentType.RequiresPayment,
                    message = "Request submitted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error submitting request: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Error submitting request: " + ex.Message });
            }
        }


        [HttpGet("Stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                _logger.LogInformation("📊 Stats request received");

                var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in");
                    return Unauthorized(new { message = "User not logged in" });
                }

                var pending = await _mongoDBService.GetCountByStatusAsync(userId.Value, "Pending");
                var processing = await _mongoDBService.GetCountByStatusAsync(userId.Value, "Processing");
                var ready = await _mongoDBService.GetCountByStatusAsync(userId.Value, "Ready");
                var total = await _mongoDBService.GetTotalCountAsync(userId.Value);

                _logger.LogInformation($"✅ Stats loaded: Pending={pending}, Processing={processing}, Ready={ready}, Total={total}");

                return Ok(new { pending, processing, ready, total });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting stats: {ex.Message}");
                return StatusCode(500, new { message = "Error loading statistics: " + ex.Message });
            }
        }

        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            try
            {
                _logger.LogInformation("📂 MyRequests endpoint called");

                var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in - returning 401");
                    return Unauthorized(new { message = "User not logged in" });
                }

                var requests = await _mongoDBService.GetRequestsByUserIdAsync(userId.Value);

                _logger.LogInformation($"✅ Returning {requests.Count} requests");

                return Ok(requests.Select(r => new
                {
                    requestId = r.Id,
                    queueNumber = r.QueueNumber,
                    documentType = r.DocumentType,
                    purpose = r.Purpose,
                    quantity = r.Quantity,
                    requestDate = r.RequestDate,
                    status = r.Status,
                    notes = r.Notes,
                    paymentStatus = r.PaymentStatus ?? string.Empty,
                    totalAmount = r.TotalAmount
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting requests: {ex.Message}");
                return StatusCode(500, new { message = "Error loading requests: " + ex.Message });
            }
        }

        [HttpGet("History")]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                _logger.LogInformation("📚 History endpoint called");

                var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in - returning 401");
                    return Unauthorized(new { message = "User not logged in" });
                }

                var history = await _mongoDBService.GetHistoryAsync(userId.Value);

                _logger.LogInformation($"✅ Returning {history.Count} history records");

                return Ok(history.Select(r => new
                {
                    requestId = r.Id,
                    queueNumber = r.QueueNumber,
                    documentType = r.DocumentType,
                    purpose = r.Purpose,
                    quantity = r.Quantity,
                    requestDate = r.RequestDate,
                    status = r.Status,
                    completedDate = r.CompletedDate
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting history: {ex.Message}");
                return StatusCode(500, new { message = "Error loading history: " + ex.Message });
            }
        }
    }

    public class DocumentRequestDto
    {
        public int DocumentTypeId { get; set; }
        public string? DocumentType { get; set; }
        public string? Purpose { get; set; }
        public int Quantity { get; set; }
        public string? DeliveryMethod { get; set; }
        public string? Notes { get; set; }
    }
}
