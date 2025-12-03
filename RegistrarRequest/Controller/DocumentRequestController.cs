using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ProjectCapstone.Helpers;
using System.Data;

namespace ProjectCapstone.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentRequestController : ControllerBase
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<DocumentRequestController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DocumentRequestController(
            IConfiguration configuration,
            ILogger<DocumentRequestController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _dbHelper = new DatabaseHelper(configuration);
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost("Submit")]
        public async Task<IActionResult> SubmitRequest([FromBody] DocumentRequestDto request)
        {
            try
            {
                _logger.LogInformation("📝 Submit request received");

                // Get user ID from session using IHttpContextAccessor
                var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");

                _logger.LogInformation($"🔐 UserId from session: {userId}");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in - no session UserId");
                    return Unauthorized(new { success = false, message = "User not logged in" });
                }

                // Generate unique queue number
                var queueNumber = SecurityHelper.GenerateQueueNumber();
                _logger.LogInformation($"🎫 Generated queue number: {queueNumber}");

                // Insert request into database
                var query = @"INSERT INTO DocumentRequests 
                    (UserId, DocumentType, Purpose, Quantity, RequestDate, Status, QueueNumber, Notes) 
                    VALUES 
                    (@UserId, @DocumentType, @Purpose, @Quantity, @RequestDate, @Status, @QueueNumber, @Notes)";

                var parameters = new Dictionary<string, object>
                {
                    { "@UserId", userId.Value },
                    { "@DocumentType", SecurityHelper.SanitizeInput(request.DocumentType ?? string.Empty) },
                    { "@Purpose", SecurityHelper.SanitizeInput(request.Purpose ?? string.Empty) },
                    { "@Quantity", request.Quantity },
                    { "@RequestDate", DateTime.Now },
                    { "@Status", "Pending" },
                    { "@QueueNumber", queueNumber },
                    { "@Notes", SecurityHelper.SanitizeInput(request.Notes ?? string.Empty) }
                };

                var rowsAffected = await _dbHelper.ExecuteNonQueryAsync(query, parameters);

                _logger.LogInformation($"✅ Document request submitted. Queue#: {queueNumber}, UserId: {userId}, Rows affected: {rowsAffected}");

                return Ok(new
                {
                    success = true,
                    queueNumber = queueNumber,
                    message = "Request submitted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error submitting request: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
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

                _logger.LogInformation($"🔐 UserId from session: {userId}");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in");
                    return Unauthorized(new { message = "User not logged in" });
                }

                var pendingQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId AND Status = 'Pending'";
                var processingQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId AND Status = 'Processing'";
                var readyQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId AND Status = 'Ready'";
                var totalQuery = "SELECT COUNT(*) FROM DocumentRequests WHERE UserId = @UserId";

                var parameters = new Dictionary<string, object> { { "@UserId", userId.Value } };

                var pendingObj = await _dbHelper.ExecuteScalarAsync(pendingQuery, parameters);
                var processingObj = await _dbHelper.ExecuteScalarAsync(processingQuery, parameters);
                var readyObj = await _dbHelper.ExecuteScalarAsync(readyQuery, parameters);
                var totalObj = await _dbHelper.ExecuteScalarAsync(totalQuery, parameters);

                var pending = Convert.ToInt32(pendingObj ?? 0);
                var processing = Convert.ToInt32(processingObj ?? 0);
                var ready = Convert.ToInt32(readyObj ?? 0);
                var total = Convert.ToInt32(totalObj ?? 0);

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

                _logger.LogInformation($"🔐 UserId from session: {userId}");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in - returning 401");
                    return Unauthorized(new { message = "User not logged in" });
                }

                var query = @"SELECT RequestId, QueueNumber, DocumentType, Purpose, Quantity, 
                             RequestDate, Status, Notes, PaymentStatus, TotalAmount 
                             FROM DocumentRequests 
                             WHERE UserId = @UserId 
                             ORDER BY RequestDate DESC";

                var parameters = new Dictionary<string, object> { { "@UserId", userId.Value } };

                _logger.LogInformation($"🔍 Querying database for UserId: {userId.Value}");

                var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

                _logger.LogInformation($"📊 Found {result.Rows.Count} requests");

                var requests = new List<object>();
                foreach (DataRow row in result.Rows)
                {
                    requests.Add(new
                    {
                        requestId = Convert.ToInt32(row["RequestId"]),
                        queueNumber = row["QueueNumber"]?.ToString() ?? string.Empty,
                        documentType = row["DocumentType"]?.ToString() ?? string.Empty,
                        purpose = row["Purpose"]?.ToString() ?? string.Empty,
                        quantity = Convert.ToInt32(row["Quantity"]),
                        requestDate = Convert.ToDateTime(row["RequestDate"]),
                        status = row["Status"]?.ToString() ?? string.Empty,
                        notes = row["Notes"]?.ToString() ?? string.Empty,
                        paymentStatus = row["PaymentStatus"]?.ToString() ?? string.Empty,
                        totalAmount = row["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(row["TotalAmount"]) : 0m
                    });
                }

                _logger.LogInformation($"✅ Returning {requests.Count} requests");

                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting requests: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
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

                _logger.LogInformation($"🔐 UserId from session: {userId}");

                if (userId == null)
                {
                    _logger.LogWarning("⚠️ User not logged in - returning 401");
                    return Unauthorized(new { message = "User not logged in" });
                }

                var query = @"SELECT RequestId, QueueNumber, DocumentType, Purpose, Quantity, 
                             RequestDate, Status, CompletedDate 
                             FROM DocumentRequests 
                             WHERE UserId = @UserId AND Status IN ('Completed', 'Cancelled')
                             ORDER BY 
                                CASE WHEN CompletedDate IS NOT NULL THEN CompletedDate ELSE RequestDate END DESC";

                var parameters = new Dictionary<string, object> { { "@UserId", userId.Value } };

                _logger.LogInformation($"🔍 Querying history for UserId: {userId.Value}");

                var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

                _logger.LogInformation($"📊 Found {result.Rows.Count} history records");

                var history = new List<object>();
                foreach (DataRow row in result.Rows)
                {
                    DateTime? completedDate = null;
                    if (row["CompletedDate"] != DBNull.Value)
                    {
                        completedDate = Convert.ToDateTime(row["CompletedDate"]);
                    }

                    history.Add(new
                    {
                        requestId = Convert.ToInt32(row["RequestId"]),
                        queueNumber = row["QueueNumber"]?.ToString() ?? string.Empty,
                        documentType = row["DocumentType"]?.ToString() ?? string.Empty,
                        purpose = row["Purpose"]?.ToString() ?? string.Empty,
                        quantity = Convert.ToInt32(row["Quantity"]),
                        requestDate = Convert.ToDateTime(row["RequestDate"]),
                        status = row["Status"]?.ToString() ?? string.Empty,
                        completedDate = completedDate
                    });
                }

                _logger.LogInformation($"✅ Returning {history.Count} history records");

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting history: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Error loading history: " + ex.Message });
            }
        }
    }

    public class DocumentRequestDto
    {
        public string? DocumentType { get; set; } = string.Empty;
        public string? Purpose { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? DeliveryMethod { get; set; } = string.Empty;
        public string? Notes { get; set; } = string.Empty;
    }
}