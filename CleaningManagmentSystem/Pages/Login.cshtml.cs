using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Models;
namespace CleaningManagmentSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly string _connectionString;

        [BindProperty]
        public string Email { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public string ErrorMessage { get; set; } = "";

        public LoginModel(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        }

        public IActionResult OnGet()
        {
            if (Request.Query.ContainsKey("logout"))
            {
                HttpContext.Session.Clear();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var role   = HttpContext.Session.GetString("UserRole");

            if (userId != null && userId > 0)
            {
                return RedirectToRolePage(role ?? "");
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Please enter email and password";
                return Page();
            }

            User? user = null;

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                user = connection.QueryFirstOrDefault<User>(
                    "SELECT id, name, email, password, role FROM users WHERE email = @Email AND is_active = 1",
                    new { Email });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Database error: {ex.Message}";
                return Page();
            }

            if (user == null || user.Password != Password)
            {
                ErrorMessage = "Invalid email or password";
                return Page();
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserRole", user.Role);

            // Also store photo_url from linked employee record (if any)
            try
            {
                using var conn2 = new MySqlConnection(_connectionString);
                var photoUrl = conn2.QueryFirstOrDefault<string>(
                    "SELECT COALESCE(photo_url,'') FROM employees WHERE user_id=@uid",
                    new { uid = user.Id });
                HttpContext.Session.SetString("UserPhoto", photoUrl ?? "");
            }
            catch { HttpContext.Session.SetString("UserPhoto", ""); }

            return RedirectToRolePage(user.Role);
        }

        private IActionResult RedirectToRolePage(string role)
        {
            var path = RoleHelper.DashboardPath(RoleHelper.NormalizeRole(role));
            return LocalRedirect("~" + path);
        }
    }
}
