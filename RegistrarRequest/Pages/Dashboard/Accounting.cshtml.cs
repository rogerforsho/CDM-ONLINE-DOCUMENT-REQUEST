using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using System.Data;
using ProjectCapstone.Services;
using MySql.Data.MySqlClient;

namespace ProjectCapstone.Pages.Dashboard
{
    public class AccountingModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<AccountingModel> _logger;
        private readonly IEmailService _emailService;

        [TempData] public string ErrorMessage { get; set; } = string.Empty;
        [TempData] public string SuccessMessage { get; set; } = string.Empty;

        public List<PaymentPlusRequest> PendingPayments { get; set; } = new();

        [BindProperty]
        public int PaymentId { get; set; }

        [BindProperty]
        public string RejectionReason { get; set; } = string.Empty;

        public AccountingModel(IConfiguration configuration, ILogger<AccountingModel> logger, IEmailService emailService)
        {
            _dbHelper = new DatabaseHelper(configuration);
            _logger = logger;
            _emailService = emailService;
        }

        private bool IsAccountingUser()
        {
            var role = HttpContext.Session.GetString("Role");
            return !string.IsNullOrEmpty(role) && (role.Equals("Staff", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Accounting", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!IsAccountingUser()) return Forbid();
            await LoadPayments();
            return Page();
        }

        public async Task<IActionResult> OnPostVerifyAsync(int PaymentId)
        {
            if (!IsAccountingUser()) return Forbid();

            // Get current user id (accounting officer)
            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            using var conn = _dbHelper.GetConnection();
            await conn.OpenAsync();
            using var trans = await conn.BeginTransactionAsync();
            try
            {
                // Approve payment
                var updateCmd = new MySqlCommand(@"UPDATE payments SET Status='Verified', VerifiedBy=@UserId, VerifiedDate=@Now
                             WHERE PaymentId=@PaymentId", conn, (MySqlTransaction)trans);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.Parameters.AddWithValue("@Now", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@PaymentId", PaymentId);
                await updateCmd.ExecuteNonQueryAsync();

                // Get RequestId
                var reqCmd = new MySqlCommand(@"SELECT RequestId FROM payments WHERE PaymentId=@PaymentId", conn, (MySqlTransaction)trans);
                reqCmd.Parameters.AddWithValue("@PaymentId", PaymentId);
                var reqIdObj = await reqCmd.ExecuteScalarAsync();
                if (reqIdObj != null && int.TryParse(reqIdObj.ToString(), out var reqId))
                {
                    // Update documentrequest
                    var updReqCmd = new MySqlCommand("UPDATE documentrequests SET CurrentStage='Pending Review', PaymentStatus='Verified' WHERE RequestId=@id", conn, (MySqlTransaction)trans);
                    updReqCmd.Parameters.AddWithValue("@id", reqId);
                    await updReqCmd.ExecuteNonQueryAsync();

                    // Insert workflow history
                    var histCmd = new MySqlCommand("INSERT INTO workflowhistory (RequestId,Stage,Action,ProcessedBy) VALUES (@r,'Pending Review','Payment Verified',@u)", conn, (MySqlTransaction)trans);
                    histCmd.Parameters.AddWithValue("@r", reqId);
                    histCmd.Parameters.AddWithValue("@u", userId);
                    await histCmd.ExecuteNonQueryAsync();

                    // Get student's email and queue number
                    var selCmd = new MySqlCommand(@"SELECT u.Email, dr.QueueNumber FROM documentrequests dr JOIN users u ON dr.UserId = u.UserId WHERE dr.RequestId = @id", conn, (MySqlTransaction)trans);
                    selCmd.Parameters.AddWithValue("@id", reqId);
                    using var reader = await selCmd.ExecuteReaderAsync();
                    string email = string.Empty;
                    string queue = string.Empty;
                    if (await reader.ReadAsync())
                    {
                        email = reader["Email"]?.ToString() ?? string.Empty;
                        queue = reader["QueueNumber"]?.ToString() ?? string.Empty;
                    }
                    reader.Close();

                    // Commit transaction first
                    await trans.CommitAsync();

                    // Send notification (fire-and-forget)
                    if (!string.IsNullOrEmpty(email))
                    {
                        _ = _emailService.SendPaymentVerificationEmailAsync(email, "Student", queue, true);
                    }
                }

                SuccessMessage = "Payment approved and request moved to review stage.";
            }
            catch (Exception ex)
            {
                try { await trans.RollbackAsync(); } catch { }
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error verifying payment");
            }
            finally
            {
                conn.Close();
            }

            await LoadPayments();
            return Page();
        }

        public async Task<IActionResult> OnPostRejectAsync(int PaymentId)
        {
            if (!IsAccountingUser()) return Forbid();

            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            using var conn = _dbHelper.GetConnection();
            await conn.OpenAsync();
            using var trans = await conn.BeginTransactionAsync();
            try
            {
                var updateCmd = new MySqlCommand(@"UPDATE payments SET Status='Rejected', VerifiedBy=@UserId, VerifiedDate=@Now, RejectionReason=@Reason
                             WHERE PaymentId=@PaymentId", conn, (MySqlTransaction)trans);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                updateCmd.Parameters.AddWithValue("@Now", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@PaymentId", PaymentId);
                updateCmd.Parameters.AddWithValue("@Reason", RejectionReason ?? string.Empty);
                await updateCmd.ExecuteNonQueryAsync();

                var reqCmd = new MySqlCommand(@"SELECT RequestId FROM payments WHERE PaymentId=@PaymentId", conn, (MySqlTransaction)trans);
                reqCmd.Parameters.AddWithValue("@PaymentId", PaymentId);
                var reqIdObj = await reqCmd.ExecuteScalarAsync();
                if (reqIdObj != null && int.TryParse(reqIdObj.ToString(), out var reqId))
                {
                    var updReqCmd = new MySqlCommand("UPDATE documentrequests SET CurrentStage='Pending Payment', PaymentStatus='Rejected' WHERE RequestId=@id", conn, (MySqlTransaction)trans);
                    updReqCmd.Parameters.AddWithValue("@id", reqId);
                    await updReqCmd.ExecuteNonQueryAsync();

                    var histCmd = new MySqlCommand("INSERT INTO workflowhistory (RequestId,Stage,Action,ProcessedBy) VALUES (@r,'Pending Payment','Payment Rejected',@u)", conn, (MySqlTransaction)trans);
                    histCmd.Parameters.AddWithValue("@r", reqId);
                    histCmd.Parameters.AddWithValue("@u", userId);
                    await histCmd.ExecuteNonQueryAsync();

                    var selCmd = new MySqlCommand(@"SELECT u.Email, dr.QueueNumber FROM documentrequests dr JOIN users u ON dr.UserId = u.UserId WHERE dr.RequestId = @id", conn, (MySqlTransaction)trans);
                    selCmd.Parameters.AddWithValue("@id", reqId);
                    using var reader = await selCmd.ExecuteReaderAsync();
                    string email = string.Empty;
                    string queue = string.Empty;
                    if (await reader.ReadAsync())
                    {
                        email = reader["Email"]?.ToString() ?? string.Empty;
                        queue = reader["QueueNumber"]?.ToString() ?? string.Empty;
                    }
                    reader.Close();

                    await trans.CommitAsync();

                    if (!string.IsNullOrEmpty(email))
                    {
                        _ = _emailService.SendPaymentVerificationEmailAsync(email, "Student", queue, false, RejectionReason);
                    }
                }

                SuccessMessage = "Payment rejected and request moved back to Pending Payment.";
            }
            catch (Exception ex)
            {
                try { await trans.RollbackAsync(); } catch { }
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error rejecting payment");
            }
            finally
            {
                conn.Close();
            }

            await LoadPayments();
            return Page();
        }

        private async Task LoadPayments()
        {
            // Join payments, requests, users to show details
            var sql = @"
                SELECT p.PaymentId, p.RequestId, p.Amount, p.PaymentMethod, p.ReferenceNumber, p.PaymentProofUrl, p.Status, p.RejectionReason,
                       dr.QueueNumber, dr.DocumentType, dr.Quantity, dr.TotalAmount,
                       CONCAT(u.FirstName, ' ', u.LastName) AS StudentName, u.Email
                FROM payments p
                LEFT JOIN documentrequests dr ON dr.RequestId = p.RequestId
                LEFT JOIN users u ON dr.UserId = u.UserId
                WHERE p.Status = 'Pending'
                ORDER BY p.PaymentDate DESC
            ";

            var dt = await _dbHelper.ExecuteQueryAsync(sql);
            PendingPayments = new List<PaymentPlusRequest>();
            foreach (DataRow row in dt.Rows)
            {
                PendingPayments.Add(new PaymentPlusRequest
                {
                    PaymentId = Convert.ToInt32(row["PaymentId"]),
                    RequestId = Convert.ToInt32(row["RequestId"]),
                    Amount = Convert.ToDecimal(row["Amount"]),
                    PaymentMethod = row["PaymentMethod"]?.ToString() ?? "",
                    ReferenceNumber = row["ReferenceNumber"]?.ToString() ?? "",
                    PaymentProofUrl = row["PaymentProofUrl"]?.ToString() ?? "",
                    Status = row["Status"]?.ToString() ?? "Pending",
                    Request = new ProjectCapstone.Models.DocumentRequest
                    {
                        QueueNumber = row["QueueNumber"]?.ToString() ?? "",
                        DocumentType = row["DocumentType"]?.ToString() ?? "",
                        Quantity = Convert.ToInt32(row["Quantity"]),
                        TotalAmount = Convert.ToDecimal(row["TotalAmount"]),
                        StudentName = row["StudentName"]?.ToString() ?? "",
                        StudentEmail = row["Email"]?.ToString() ?? ""
                    },
                    RejectionReason = row["RejectionReason"]?.ToString()
                });
            }
        }

        public class PaymentPlusRequest : Payment
        {
            public ProjectCapstone.Models.DocumentRequest? Request { get; set; }
            public string? RejectionReason { get; set; }
        }
    }
}
