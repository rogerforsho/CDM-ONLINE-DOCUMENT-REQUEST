using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using System.ComponentModel.DataAnnotations;

namespace ProjectCapstone.Pages
{
    public class LoginModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<LoginModel> _logger;

        [BindProperty]
        [Required(ErrorMessage = "Student Number or Email is required")]
        public string StudentNumber { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [BindProperty]
        public bool RememberMe { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger)
        {
            _dbHelper = new DatabaseHelper(configuration);
            _logger = logger;
        }

        public void OnGet()
        {
            // Check if user is already logged in
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                Response.Redirect("/Dashboard");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Sanitize input
                var sanitizedInput = SecurityHelper.SanitizeInput(StudentNumber);

                // Query to find user by student number or email
                var query = @"SELECT UserId, StudentNumber, FirstName, LastName, Email, 
                                 PasswordHash, ContactNumber, Course, YearLevel, Role, IsActive 
                                 FROM Users 
                                 WHERE (StudentNumber = @Input OR Email = @Input) AND IsActive = 1";

                var parameters = new Dictionary<string, object>
                    {
                        { "@Input", sanitizedInput }
                    };

                var result = await _dbHelper.ExecuteQueryAsync(query, parameters);

                if (result.Rows.Count == 0)
                {
                    ErrorMessage = "Invalid student number/email or password.";
                    _logger.LogWarning($"Failed login attempt for: {sanitizedInput}");
                    return Page();
                }

                var row = result.Rows[0];
                var storedHash = row["PasswordHash"].ToString();

                // Verify password
                if (!SecurityHelper.VerifyPassword(Password, storedHash))
                {
                    ErrorMessage = "Invalid student number/email or password.";
                    _logger.LogWarning($"Failed login attempt (wrong password) for: {sanitizedInput}");
                    return Page();
                }

                // Login successful - create session
                var userId = Convert.ToInt32(row["UserId"]);
                var role = row["Role"].ToString();
                var fullName = $"{row["FirstName"]} {row["LastName"]}";

                HttpContext.Session.SetInt32("UserId", userId);
                HttpContext.Session.SetString("Role", role);
                HttpContext.Session.SetString("FullName", fullName);
                HttpContext.Session.SetString("StudentNumber", row["StudentNumber"].ToString());
                HttpContext.Session.SetString("Email", row["Email"].ToString());

                // Update last login time
                var updateQuery = "UPDATE Users SET LastLogin = @LoginTime WHERE UserId = @UserId";
                var updateParams = new Dictionary<string, object>
                    {
                        { "@LoginTime", DateTime.Now },
                        { "@UserId", userId }
                    };
                await _dbHelper.ExecuteNonQueryAsync(updateQuery, updateParams);

                // Log session
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                var logQuery = @"INSERT INTO SessionLogs (UserId, IpAddress, UserAgent) 
                                    VALUES (@UserId, @IpAddress, @UserAgent)";
                var logParams = new Dictionary<string, object>
                    {
                        { "@UserId", userId },
                        { "@IpAddress", ipAddress },
                        { "@UserAgent", userAgent }
                    };
                await _dbHelper.ExecuteNonQueryAsync(logQuery, logParams);

                _logger.LogInformation($"User {fullName} logged in successfully");

                // Redirect based on role
                if (role == "Admin" || role == "Staff")
                {
                    return RedirectToPage("/Dashboard/Admin");
                }
                else
                {
                    return RedirectToPage("/Dashboard/Student");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error: {ex.Message}");
                ErrorMessage = "An error occurred during login. Please try again.";
                return Page();
            }
        }
    }
}