using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using ProjectCapstone.Services;
using System.ComponentModel.DataAnnotations;

namespace ProjectCapstone.Pages
{
    public class LoginModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<LoginModel> _logger;

        [BindProperty]
        [Required(ErrorMessage = "Student Number or Email is required")]
        public string StudentNumber { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        [BindProperty]
        public string RoleType { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        public LoginModel(MongoDBService mongoDBService, ILogger<LoginModel> logger)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
        }

        public void OnGet()
        {
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
                var sanitizedInput = SecurityHelper.SanitizeInput(StudentNumber);

                // Find user by student number or email
                var user = await _mongoDBService.GetUserByStudentNumberOrEmailAsync(sanitizedInput);

                if (user == null)
                {
                    ErrorMessage = "Invalid student number/email or password.";
                    _logger.LogWarning($"Failed login attempt for: {sanitizedInput}");
                    return Page();
                }

                // Verify password
                if (string.IsNullOrEmpty(user.PasswordHash) ||
                    !SecurityHelper.VerifyPassword(Password, user.PasswordHash))
                {
                    ErrorMessage = "Invalid student number/email or password.";
                    _logger.LogWarning($"Failed login attempt (wrong password) for: {sanitizedInput}");
                    return Page();
                }

                // Validate role if specified
                if (!string.IsNullOrEmpty(RoleType))
                {
                    if (RoleType.ToLower() == "student" && (user.Role == "Admin" || user.Role == "Staff"))
                    {
                        ErrorMessage = "This account is not registered as a student. Please use Admin login.";
                        _logger.LogWarning($"Role mismatch: Student login attempted for admin account: {sanitizedInput}");
                        return Page();
                    }

                    if (RoleType.ToLower() == "admin" && user.Role == "Student")
                    {
                        ErrorMessage = "This account is not registered as admin/staff. Please use Student login.";
                        _logger.LogWarning($"Role mismatch: Admin login attempted for student account: {sanitizedInput}");
                        return Page();
                    }
                }

                // Create session
                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("Role", user.Role);
                HttpContext.Session.SetString("FullName", fullName);
                HttpContext.Session.SetString("StudentNumber", user.StudentNumber);
                HttpContext.Session.SetString("Email", user.Email);

                // Update last login
                await _mongoDBService.UpdateUserLastLoginAsync(user.UserId);

                // Log session
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                await _mongoDBService.CreateSessionLogAsync(new SessionLog
                {
                    UserId = user.UserId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                });

                _logger.LogInformation($"User {fullName} logged in successfully");

                // Redirect based on role
                if (user.Role == "Admin" || user.Role == "Staff")
                {
                    return RedirectToPage("/AdminPin");
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
