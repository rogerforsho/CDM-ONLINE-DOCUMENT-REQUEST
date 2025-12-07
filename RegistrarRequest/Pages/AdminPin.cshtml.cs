using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProjectCapstone.Pages
{
    public class AdminPinModel : PageModel
    {
        private readonly ILogger<AdminPinModel> _logger;

        // ⚠️ CHANGE THIS TO YOUR DESIRED PIN
        private const string ADMIN_PIN = "1234";
        private const int MAX_ATTEMPTS = 3;

        public string AdminName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int AttemptsRemaining { get; set; } = MAX_ATTEMPTS;

        public AdminPinModel(ILogger<AdminPinModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Check if user is admin/staff
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "Staff")
            {
                return RedirectToPage("/Dashboard/Student");
            }

            // Check if already verified
            var pinVerified = HttpContext.Session.GetString("PinVerified");
            if (pinVerified == "true")
            {
                return RedirectToPage("/Dashboard/Admin");
            }

            AdminName = HttpContext.Session.GetString("FullName") ?? "Admin";
            AttemptsRemaining = HttpContext.Session.GetInt32("PinAttempts") ?? MAX_ATTEMPTS;

            return Page();
        }

        public IActionResult OnPost(string pin1, string pin2, string pin3, string pin4)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            AdminName = HttpContext.Session.GetString("FullName") ?? "Admin";

            // Get current attempts
            var attempts = HttpContext.Session.GetInt32("PinAttempts") ?? MAX_ATTEMPTS;

            // Combine PIN
            string enteredPin = $"{pin1}{pin2}{pin3}{pin4}";

            // Verify PIN
            if (enteredPin == ADMIN_PIN)
            {
                // ✅ PIN CORRECT
                HttpContext.Session.SetString("PinVerified", "true");
                HttpContext.Session.Remove("PinAttempts");

                _logger.LogInformation($"✅ Admin PIN verified for user {userId}");

                return RedirectToPage("/Dashboard/Admin");
            }
            else
            {
                // ❌ PIN INCORRECT
                attempts--;
                HttpContext.Session.SetInt32("PinAttempts", attempts);
                AttemptsRemaining = attempts;

                _logger.LogWarning($"⚠️ Failed PIN attempt for user {userId}. Attempts remaining: {attempts}");

                if (attempts <= 0)
                {
                    // Too many failed attempts - logout
                    _logger.LogWarning($"🚫 Max PIN attempts exceeded for user {userId}. Logging out.");
                    HttpContext.Session.Clear();
                    TempData["ErrorMessage"] = "Too many failed PIN attempts. Please login again.";
                    return RedirectToPage("/Login");
                }

                ErrorMessage = $"❌ Incorrect PIN. {attempts} attempt(s) remaining.";
                return Page();
            }
        }
    }
}
