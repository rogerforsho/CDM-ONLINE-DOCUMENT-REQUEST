using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using System.Data;
using ProjectCapstone.Models;


namespace ProjectCapstone.Pages.DocumentRequest
{
    public class PaymentModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
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

        public ProjectCapstone.Models.DocumentRequest? Request { get; set; }

        public ProjectCapstone.Models.Payment? ExistingPayment { get; set; }


        public PaymentModel(IConfiguration configuration, ILogger<PaymentModel> logger, IWebHostEnvironment environment)
        {
            _dbHelper = new DatabaseHelper(configuration);
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

            // Load request details
            await LoadRequestDetails(userId.Value);

            if (Request == null)
            {
                ErrorMessage = "Request not found or you don't have permission to view it.";
                return Page();
            }

            // Load existing payment if any
            await LoadExistingPayment();

            // Prefill form fields from existing payment if present
            if (ExistingPayment != null)
            {
                PaymentMethod = ExistingPayment.PaymentMethod ?? string.Empty;
                ReferenceNumber = ExistingPayment.ReferenceNumber ?? string.Empty;
                // Notes left as-is; you may consider showing ExistingPayment.PaymentDate etc. in view
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

            // Load request first to validate
            await LoadRequestDetails(userId.Value);
            if (Request == null)
            {
                ErrorMessage = "Request not found or you don't have permission to view it.";
                return Page();
            }

            // Load existing payment (if any)
            await LoadExistingPayment();

            // Basic form validation
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

                if (PaymentProofFile.Length > 5 * 1024 * 1024) // 5MB
                {
                    ErrorMessage = "File size must be less than 5MB.";
                    await LoadRequestDetails(userId.Value);
                    return Page();
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Generate unique filename
                var fileName = $"{RequestId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsPath, fileName);
                var relativeFilePath = $"/uploads/receipts/{fileName}";

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PaymentProofFile.CopyToAsync(stream);
                }

                // Get request amount (re-read to ensure accuracy)
                var amountQuery = "SELECT TotalAmount FROM documentrequests WHERE RequestId = @RequestId";
                var amountParams = new Dictionary<string, object> { { "@RequestId", RequestId } };
                var amountResult = await _dbHelper.ExecuteQueryAsync(amountQuery, amountParams);
                var amount = Convert.ToDecimal(amountResult.Rows[0]["TotalAmount"]);

                if (ExistingPayment != null)
                {
                    // If payment already verified, do not allow updates
                    if (string.Equals(ExistingPayment.Status, "Verified", StringComparison.OrdinalIgnoreCase))
                    {
                        ErrorMessage = "Payment already verified. No further uploads allowed.";
                        return RedirectToPage("/Dashboard/Student");
                    }

                    // Update existing payment (pending or rejected)
                    var updatePayment = @"UPDATE payments SET Amount=@Amount, PaymentMethod=@PaymentMethod, ReferenceNumber=@ReferenceNumber,
                                            PaymentProofUrl=@PaymentProofUrl, Status=@Status, PaymentDate=@PaymentDate, RejectionReason=NULL, UpdatedDate=@Now
                                         WHERE PaymentId=@PaymentId";

                    var updParams = new Dictionary<string, object>
                    {
                        { "@Amount", amount },
                        { "@PaymentMethod", PaymentMethod },
                        { "@ReferenceNumber", ReferenceNumber },
                        { "@PaymentProofUrl", relativeFilePath },
                        { "@Status", "Pending" },
                        { "@PaymentDate", DateTime.Now },
                        { "@Now", DateTime.Now },
                        { "@PaymentId", ExistingPayment.PaymentId }
                    };

                    await _dbHelper.ExecuteNonQueryAsync(updatePayment, updParams);
                }
                else
                {
                    // Insert payment record
                    var insertPaymentQuery = @"INSERT INTO payments 
                    (RequestId, Amount, PaymentMethod, ReferenceNumber, PaymentProofUrl, Status, PaymentDate)
                    VALUES (@RequestId, @Amount, @PaymentMethod, @ReferenceNumber, @PaymentProofUrl, @Status, @PaymentDate)";

                    var paymentParams = new Dictionary<string, object>
                    {
                        { "@RequestId", RequestId },
                        { "@Amount", amount },
                        { "@PaymentMethod", PaymentMethod },
                        { "@ReferenceNumber", ReferenceNumber },
                        { "@PaymentProofUrl", relativeFilePath },
                        { "@Status", "Pending" },
                        { "@PaymentDate", DateTime.Now }
                    };

                    await _dbHelper.ExecuteNonQueryAsync(insertPaymentQuery, paymentParams);
                }

                // Update request status - set to Pending Verification so staff must confirm
                var updateRequestQuery = @"UPDATE documentrequests 
                    SET CurrentStage = @CurrentStage, PaymentStatus = @PaymentStatus 
                    WHERE RequestId = @RequestId";

                var updateParams = new Dictionary<string, object>
                {
                    { "@CurrentStage", "Payment Verification" },
                    { "@PaymentStatus", "Pending Verification" },
                    { "@RequestId", RequestId }
                };

                await _dbHelper.ExecuteNonQueryAsync(updateRequestQuery, updateParams);

                // Create workflow history
                var historyQuery = @"INSERT INTO workflowhistory (RequestId, Stage, Action, Comments, ProcessedBy)
                                    VALUES (@RequestId, @Stage, @Action, @Comments, @ProcessedBy)";
                var historyParams = new Dictionary<string, object>
                {
                    { "@RequestId", RequestId },
                    { "@Stage", "Payment Verification" },
                    { "@Action", "Payment Proof Uploaded" },
                    { "@Comments", $"Payment Method: {PaymentMethod}, Ref: {ReferenceNumber}. {Notes}" },
                    { "@ProcessedBy", userId }
                };
                await _dbHelper.ExecuteNonQueryAsync(historyQuery, historyParams);

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
                var query = @"SELECT dr.*, dt.DocumentName, dt.Amount 
                             FROM documentrequests dr
                             LEFT JOIN documenttypes dt ON dr.DocumentTypeId = dt.DocumentTypeId
                             WHERE dr.RequestId = @RequestId AND dr.UserId = @UserId";

                var parameters = new Dictionary<string, object>
                {
                    { "@RequestId", RequestId },
                    { "@UserId", userId }
                };

                var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    Request = new ProjectCapstone.Models.DocumentRequest
                    {
                        RequestId = Convert.ToInt32(row["RequestId"]),
                        QueueNumber = row["QueueNumber"]?.ToString() ?? string.Empty,
                        DocumentType = row["DocumentType"]?.ToString() ?? string.Empty,
                        Quantity = Convert.ToInt32(row["Quantity"]),
                        TotalAmount = Convert.ToDecimal(row["TotalAmount"]),
                        CurrentStage = row["CurrentStage"]?.ToString() ?? string.Empty,
                        PaymentStatus = row["PaymentStatus"]?.ToString() ?? string.Empty
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
                var q = @"SELECT * FROM payments WHERE RequestId = @RequestId ORDER BY PaymentDate DESC LIMIT 1";
                var dt = await _dbHelper.ExecuteQueryAsync(q, new() { { "@RequestId", RequestId } });
                if (dt.Rows.Count > 0)
                {
                    var r = dt.Rows[0];
                    ExistingPayment = new ProjectCapstone.Models.Payment
                    {
                        PaymentId = Convert.ToInt32(r["PaymentId"]),
                        RequestId = Convert.ToInt32(r["RequestId"]),
                        Amount = Convert.ToDecimal(r["Amount"]),
                        PaymentMethod = r["PaymentMethod"]?.ToString() ?? string.Empty,
                        ReferenceNumber = r["ReferenceNumber"]?.ToString(),
                        PaymentProofUrl = r["PaymentProofUrl"]?.ToString(),
                        Status = r["Status"]?.ToString() ?? "Pending",
                        VerifiedBy = r["VerifiedBy"] != DBNull.Value ? Convert.ToInt32(r["VerifiedBy"]) : (int?)null,
                        VerifiedDate = r["VerifiedDate"] != DBNull.Value ? Convert.ToDateTime(r["VerifiedDate"]) : (DateTime?)null,
                        RejectionReason = r["RejectionReason"]?.ToString(),
                        PaymentDate = r["PaymentDate"] != DBNull.Value ? Convert.ToDateTime(r["PaymentDate"]) : DateTime.MinValue
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading existing payment: {ex.Message}");
            }
        }
    }
}
