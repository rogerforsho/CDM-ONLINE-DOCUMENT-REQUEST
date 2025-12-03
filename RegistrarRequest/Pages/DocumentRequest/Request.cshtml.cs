using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using System.Data;

namespace ProjectCapstone.Pages.DocumentRequest
{
    public class RequestModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
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

        public List<DocumentType> DocumentTypes { get; set; } = new();

        public RequestModel(IConfiguration configuration, ILogger<RequestModel> logger)
        {
            _dbHelper = new DatabaseHelper(configuration);
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Load document types
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
                var docTypeQuery = "SELECT * FROM documenttypes WHERE DocumentTypeId = @DocumentTypeId AND IsActive = 1";
                var docTypeParams = new Dictionary<string, object> { { "@DocumentTypeId", DocumentTypeId } };
                var docTypeResult = await _dbHelper.ExecuteQueryAsync(docTypeQuery, docTypeParams);

                if (docTypeResult.Rows.Count == 0)
                {
                    ErrorMessage = "Invalid document type selected.";
                    await LoadDocumentTypes();
                    return Page();
                }

                var docTypeRow = docTypeResult.Rows[0];
                var requiresPayment = docTypeRow["RequiresPayment"] != DBNull.Value && (docTypeRow["RequiresPayment"].ToString() == "1" || docTypeRow["RequiresPayment"].ToString().ToLower() == "true");
                var amount = docTypeRow["Amount"] != DBNull.Value ? Convert.ToDecimal(docTypeRow["Amount"]) : 0m;
                var processingDays = docTypeRow["ProcessingDays"] != DBNull.Value ? Convert.ToInt32(docTypeRow["ProcessingDays"]) : 0;
                var documentName = docTypeRow["DocumentName"]?.ToString() ?? string.Empty;

                // Calculate total and dates
                decimal totalAmount = amount * Quantity;
                var targetReleaseDate = DateTime.Now.AddDays(processingDays);

                // Generate queue number
                string queueNumber = $"CDM-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";

                // Determine initial stage and payment status
                string currentStage = requiresPayment ? "Pending Payment" : "Pending Review";
                string paymentStatus = requiresPayment ? "Pending" : "Not Required";

                // Insert document request
                var insertQuery = @"INSERT INTO documentrequests 
                    (UserId, DocumentTypeId, DocumentType, Purpose, Quantity, TotalAmount, 
                     PaymentStatus, CurrentStage, Status, QueueNumber, RequestDate, TargetReleaseDate)
                    VALUES (@UserId, @DocumentTypeId, @DocumentType, @Purpose, @Quantity, @TotalAmount,
                            @PaymentStatus, @CurrentStage, @Status, @QueueNumber, @RequestDate, @TargetReleaseDate)";

                var insertParams = new Dictionary<string, object>
                {
                    { "@UserId", userId },
                    { "@DocumentTypeId", DocumentTypeId },
                    { "@DocumentType", documentName },
                    { "@Purpose", Purpose },
                    { "@Quantity", Quantity },
                    { "@TotalAmount", totalAmount },
                    { "@PaymentStatus", paymentStatus },
                    { "@CurrentStage", currentStage },
                    { "@Status", "Active" },
                    { "@QueueNumber", queueNumber },
                    { "@RequestDate", DateTime.Now },
                    { "@TargetReleaseDate", targetReleaseDate }
                };

                await _dbHelper.ExecuteNonQueryAsync(insertQuery, insertParams);

                // Get the inserted RequestId
                var getIdQuery = "SELECT LAST_INSERT_ID() as RequestId";
                var idResult = await _dbHelper.ExecuteQueryAsync(getIdQuery);
                var requestId = Convert.ToInt32(idResult.Rows[0]["RequestId"]);

                // Create workflow history entry
                var historyQuery = @"INSERT INTO workflowhistory (RequestId, Stage, Action, Comments, ProcessedBy)
                                    VALUES (@RequestId, @Stage, @Action, @Comments, @ProcessedBy)";
                var historyParams = new Dictionary<string, object>
                {
                    { "@RequestId", requestId },
                    { "@Stage", currentStage },
                    { "@Action", "Request Submitted" },
                    { "@Comments", $"Document requested: {documentName}, Quantity: {Quantity}" },
                    { "@ProcessedBy", userId }
                };
                await _dbHelper.ExecuteNonQueryAsync(historyQuery, historyParams);

                _logger.LogInformation($"Document request created: RequestId={requestId}, UserId={userId}");

                // Redirect based on payment requirement
                if (requiresPayment)
                {
                    return RedirectToPage("/DocumentRequest/Payment", new { requestId });
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
                var query = @"SELECT * FROM documenttypes WHERE IsActive = 1 ORDER BY Category, DocumentName";
                var result = await _dbHelper.ExecuteQueryAsync(query);

                DocumentTypes = new List<DocumentType>();
                foreach (DataRow row in result.Rows)
                {
                    bool requiresPayment = false;
                    if (row["RequiresPayment"] != DBNull.Value)
                    {
                        var rp = row["RequiresPayment"].ToString() ?? string.Empty;
                        requiresPayment = rp == "1" || rp.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }

                    decimal amount = 0m;
                    if (row["Amount"] != DBNull.Value)
                    {
                        decimal.TryParse(row["Amount"].ToString(), out amount);
                    }

                    int processingDays = 0;
                    if (row["ProcessingDays"] != DBNull.Value)
                    {
                        int.TryParse(row["ProcessingDays"].ToString(), out processingDays);
                    }

                    DocumentTypes.Add(new DocumentType
                    {
                        DocumentTypeId = row["DocumentTypeId"] != DBNull.Value ? Convert.ToInt32(row["DocumentTypeId"]) : 0,
                        DocumentName = row["DocumentName"]?.ToString() ?? string.Empty,
                        Description = row["Description"]?.ToString(),
                        RequiresPayment = requiresPayment,
                        Amount = amount,
                        ProcessingDays = processingDays,
                        RequiresClearance = row["RequiresClearance"] != DBNull.Value && Convert.ToBoolean(row["RequiresClearance"]),
                        Category = row["Category"]?.ToString() ?? string.Empty,
                        IsActive = row["IsActive"] != DBNull.Value && Convert.ToBoolean(row["IsActive"])
                    });
                }

                if (DocumentTypes.Count == 0)
                {
                    _logger.LogWarning("No active document types found in database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading document types: {ex.Message}");
                DocumentTypes = new List<DocumentType>();
            }
        }
    }
}
