using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProjectCapstone.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var fullName = HttpContext.Session.GetString("FullName");

            if (userId != null)
            {
                _logger.LogInformation($"User {fullName} (ID: {userId}) logged out");
            }

            // Clear all session data
            HttpContext.Session.Clear();

            // Redirect to login with success message
            TempData["SuccessMessage"] = "You have been logged out successfully.";

            return RedirectToPage("/Login");
        }
    }
}