using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using System.ComponentModel.DataAnnotations;

namespace ProjectCapstone.Pages
{
    public class SignupModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<SignupModel> _logger;

        [BindProperty]
        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Student number is required")]
        [RegularExpression(@"^[A-Z0-9-]+$", ErrorMessage = "Invalid student number format")]
        public string StudentNumber { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Contact number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string ContactNumber { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Course is required")]
        public string Course { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Year level is required")]
        public string YearLevel { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, number and special character")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        public SignupModel(IConfiguration configuration, ILogger<SignupModel> logger)
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
                // Sanitize inputs
                var sanitizedStudentNumber = SecurityHelper.SanitizeInput(StudentNumber);
                var sanitizedEmail = SecurityHelper.SanitizeInput((Email ?? string.Empty).ToLower());
                var sanitizedFirstName = SecurityHelper.SanitizeInput(FirstName);
                var sanitizedLastName = SecurityHelper.SanitizeInput(LastName);

                // Check if student number already exists
                var checkStudentQuery = "SELECT COUNT(*) FROM Users WHERE StudentNumber = @StudentNumber";
                var studentParams = new Dictionary<string, object>
                {
                    { "@StudentNumber", sanitizedStudentNumber }
                };
                var studentExistsObj = await _dbHelper.ExecuteScalarAsync(checkStudentQuery, studentParams);
                var studentExists = Convert.ToInt32(studentExistsObj ?? 0);

                if (studentExists > 0)
                {
                    ErrorMessage = "Student number already registered.";
                    return Page();
                }

                // Check if email already exists
                var checkEmailQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                var emailParams = new Dictionary<string, object>
                {
                    { "@Email", sanitizedEmail }
                };
                var emailExistsObj = await _dbHelper.ExecuteScalarAsync(checkEmailQuery, emailParams);
                var emailExists = Convert.ToInt32(emailExistsObj ?? 0);

                if (emailExists > 0)
                {
                    ErrorMessage = "Email address already registered.";
                    return Page();
                }

                // Hash the password
                var passwordHash = SecurityHelper.HashPassword(Password);

                // Insert new user
                var insertQuery = @"INSERT INTO Users 
                    (StudentNumber, FirstName, LastName, Email, PasswordHash, ContactNumber, Course, YearLevel, Role, IsActive) 
                    VALUES 
                    (@StudentNumber, @FirstName, @LastName, @Email, @PasswordHash, @ContactNumber, @Course, @YearLevel, 'Student', 1)";

                var insertParams = new Dictionary<string, object>
                {
                    { "@StudentNumber", sanitizedStudentNumber },
                    { "@FirstName", sanitizedFirstName },
                    { "@LastName", sanitizedLastName },
                    { "@Email", sanitizedEmail },
                    { "@PasswordHash", passwordHash },
                    { "@ContactNumber", ContactNumber ?? string.Empty },
                    { "@Course", Course ?? string.Empty },
                    { "@YearLevel", YearLevel ?? string.Empty }
                };

                await _dbHelper.ExecuteNonQueryAsync(insertQuery, insertParams);

                _logger.LogInformation($"New user registered: {sanitizedStudentNumber}");

                // Set success message and redirect to login
                SuccessMessage = "Account created successfully! Please login.";
                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Signup error: {ex.Message}");
                ErrorMessage = "An error occurred during registration. Please try again.";
                return Page();
            }
        }
    }
}