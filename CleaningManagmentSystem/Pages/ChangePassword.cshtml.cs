using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages
{
    public class ChangePasswordModel : PageModel
    {
        private readonly string _cs;
        public ChangePasswordModel(IConfiguration cfg) => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        [BindProperty]
        public string NewPassword { get; set; } = "";

        [BindProperty]
        public string ConfirmPassword { get; set; } = "";

        public string ErrorMessage { get; set; } = "";
        
        public IActionResult OnGet()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || userId <= 0) return RedirectToPage("/Login");
            return Page();
        }

        public IActionResult OnPost()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (userId == null || userId <= 0) return RedirectToPage("/Login");

            if (string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
            {
                ErrorMessage = "Please fill in all fields.";
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            if (NewPassword == "Yeka@1234" || NewPassword == "Yeka@123")
            {
                ErrorMessage = "You cannot reuse a default password. Please choose a unique password.";
                return Page();
            }

            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute(
                    "UPDATE users SET password=@pw, is_default_password=0, updated_at=NOW() WHERE id=@uid",
                    new { pw = NewPassword, uid = userId });

                // Clear the forced-change flag from the session
                HttpContext.Session.SetString("MustChangePassword", "0");

                // Redirect to their dashboard
                var path = CleaningManagmentSystem.Models.RoleHelper.DashboardPath(CleaningManagmentSystem.Models.RoleHelper.NormalizeRole(role ?? ""));
                return LocalRedirect("~" + path);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Database error: {ex.Message}";
                return Page();
            }

        }
    }
}
