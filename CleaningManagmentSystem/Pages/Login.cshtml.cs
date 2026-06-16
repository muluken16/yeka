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
            Console.WriteLine("[Login] OnGet called");
            
            if (Request.Query.ContainsKey("logout"))
            {
                Console.WriteLine("[Login] Logout requested");
                HttpContext.Session.Clear();
            }
            
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("UserRole");
            
            Console.WriteLine($"[Login] Session - UserId: {userId}, UserName: {userName}, Role: {role}");
            
            if (userId != null && userId > 0)
            {
                Console.WriteLine($"[Login] User already logged in, redirecting to {role} dashboard");
                return RedirectToRolePage(role ?? "");
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            Console.WriteLine($"[Login] OnPost called with Email: {Email}");
            
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Please enter email and password";
                return Page();
            }

            var demoUsers = new Dictionary<string, (int id, string name, string role, string password)>
            {
                { "superadmin@yeka.et", (1, "Super Admin", "SuperAdmin", "admin123") },
                { "manager@yeka.et", (2, "Manager", "Manager", "manager123") },
                { "staff@yeka.et", (3, "Staff", "Staff", "staff123") },
                { "cleaner@yeka.et", (4, "Cleaner", "Cleaner", "clean123") },
                { "wereda@addis.gov.et", (5, "Wereda Mahberat", "Wereda_Mahberat", "wereda123") },
                { "dispatch@yeka.et", (6, "Dispatch Officer", "Dispatch_Officer", "dispatch123") },
                { "driver1@yeka.et", (7, "Driver", "Driver", "driver123") }
            };

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
                Console.WriteLine($"[Login] Database error: {ex.Message}");
            }

            if (user == null && demoUsers.TryGetValue(Email, out var demo))
            {
                if (demo.password == Password)
                {
                    Console.WriteLine($"[Login] Demo login successful for: {demo.name}, Role: {demo.role}");
                    HttpContext.Session.SetInt32("UserId", demo.id);
                    HttpContext.Session.SetString("UserName", demo.name);
                    HttpContext.Session.SetString("UserRole", demo.role.Replace("_", " ").Trim());
                    return RedirectToRolePage(demo.role.Replace("_", " ").Trim());
                }
            }

            if (user == null)
            {
                ErrorMessage = "Invalid email or password";
                return Page();
            }

            if (user.Password != Password)
            {
                ErrorMessage = "Invalid email or password";
                return Page();
            }

            Console.WriteLine($"[Login] Login successful for: {user.Name}, Role: {user.Role}");
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserRole", user.Role);
            
            return RedirectToRolePage(user.Role);
        }

        private IActionResult RedirectToRolePage(string role)
        {
            return role?.ToLower() switch
            {
                "superadmin" => RedirectToPage("/Dashboard/SuperAdmin"),
                "manager" => RedirectToPage("/Dashboard/Manager"),
                "staff" => RedirectToPage("/Dashboard/Staff/Index"),
                "cleaner" => RedirectToPage("/Dashboard/Cleaner"),
                "user" => RedirectToPage("/Dashboard/User"),
                "wereda mahberat" => RedirectToPage("/Dashboard/WeredaMahberat"),
                "dispatch officer" => RedirectToPage("/Dashboard/DispatchOfficer"),
                "driver" => RedirectToPage("/Dashboard/Driver"),
                _ => RedirectToPage("/Index")
            };
        }
    }
}