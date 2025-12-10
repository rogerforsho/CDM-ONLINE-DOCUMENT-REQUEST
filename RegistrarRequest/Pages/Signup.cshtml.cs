using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectCapstone.Helpers;
using ProjectCapstone.Models;
using ProjectCapstone.Services;
using System.ComponentModel.DataAnnotations;

namespace ProjectCapstone.Pages
{
    public class SignupModel : PageModel
    {
        private readonly MongoDBService _mongoDBService;
        private readonly ILogger<SignupModel> _logger;

        [BindProperty]
        [Required(ErrorMessage = "Student Number is required")]
        public string StudentNumber { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "First Name is required")]
        public string FirstName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Last Name is required")]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public int DepartmentId { get; set; }

        public List<Department> Departments { get; set; } = new();


        [BindProperty]
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [BindProperty]
        [Phone(ErrorMessage = "Invalid contact number")]
        public string? ContactNumber { get; set; }

        [BindProperty]
        public string? Course { get; set; }

        [BindProperty]
        public string? YearLevel { get; set; }

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        public SignupModel(MongoDBService mongoDBService, ILogger<SignupModel> logger)
        {
            _mongoDBService = mongoDBService;
            _logger = logger;
        }

        

        public async Task OnGetAsync()
        {
            Departments = await _mongoDBService.GetActiveDepartmentsAsync();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Departments = await _mongoDBService.GetActiveDepartmentsAsync();
                return Page();
            }

            try
            {
                // Sanitize inputs
                var sanitizedStudentNumber = SecurityHelper.SanitizeInput(StudentNumber);
                var sanitizedEmail = SecurityHelper.SanitizeInput(Email);

                // Check if student number already exists
                var existingStudentNumber = await _mongoDBService.GetUserByStudentNumberAsync(sanitizedStudentNumber);
                if (existingStudentNumber != null)
                {
                    Departments = await _mongoDBService.GetActiveDepartmentsAsync();
                    return Page();
                }

                // Check if email already exists
                var existingEmail = await _mongoDBService.GetUserByEmailAsync(sanitizedEmail);
                if (existingEmail != null)
                {
                    Departments = await _mongoDBService.GetActiveDepartmentsAsync();
                    return Page();
                }
                var existingStudent = await _mongoDBService.GetUserByStudentNumberAsync(StudentNumber);
                if (existingStudent != null)
                {
                    ModelState.AddModelError(string.Empty, "Student number already registered");
                    Departments = await _mongoDBService.GetActiveDepartmentsAsync();
                    return Page();
                }

                // Get department info
                var department = await _mongoDBService.GetDepartmentByIdAsync(DepartmentId);
                if (department == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid department selected");
                    Departments = await _mongoDBService.GetActiveDepartmentsAsync();
                    return Page();
                }



                // Hash password
                var passwordHash = SecurityHelper.HashPassword(Password);

                // Create new user
                var newUser = new User
                {
                    StudentNumber = sanitizedStudentNumber,
                    FirstName = SecurityHelper.SanitizeInput(FirstName),
                    LastName = SecurityHelper.SanitizeInput(LastName),
                    Email = sanitizedEmail,
                    PasswordHash = passwordHash,
                    ContactNumber = SecurityHelper.SanitizeInput(ContactNumber ?? string.Empty),
                    Course = SecurityHelper.SanitizeInput(Course ?? string.Empty),
                    YearLevel = SecurityHelper.SanitizeInput(YearLevel ?? string.Empty),
                    Role = "Student",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    DepartmentId = DepartmentId,
                    DepartmentCode = (await _mongoDBService.GetDepartmentByIdAsync(DepartmentId))?.DepartmentCode ?? "",
                    DepartmentName = (await _mongoDBService.GetDepartmentByIdAsync(DepartmentId))?.DepartmentName ?? "",

                };

                await _mongoDBService.CreateUserAsync(newUser);

                _logger.LogInformation($"New user registered: {sanitizedStudentNumber}");

                SuccessMessage = "Registration successful! Please login.";
                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Registration error: {ex.Message}");
                ErrorMessage = "An error occurred during registration. Please try again.";
                return Page();
            }
        }
    }
}
