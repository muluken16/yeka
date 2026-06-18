using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;
using UserModel = CleaningManagmentSystem.Models.User;

namespace CleaningManagmentSystem.Pages.Dashboard.SuperAdmin
{
    public class UsersModel : PageModel
    {
        private readonly string _cs;

        public List<UserModel> UsersList  { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public string SearchQuery    { get; set; } = "";

        // Employees registered via HR — shown in "Link to Employee" dropdown when creating a user
        public List<EmployeeQuickDto> AvailableEmployees { get; set; } = new();

        public record EmployeeQuickDto(int Id, string Name, string LastName, string Email, string Phone);

        public static readonly HashSet<string> MobileRoles = new(StringComparer.OrdinalIgnoreCase)
            { "driver", "outsource", "PrivateCompanyRep", "manager" };

        public UsersModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // GET only — all mutations go through /admin/users/* controller actions
        public IActionResult OnGet(
            [FromQuery] string? searchQuery = null,
            [FromQuery] string? ok = null,
            [FromQuery] string? err = null)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            SearchQuery    = searchQuery ?? "";
            SuccessMessage = ok  ?? "";
            ErrorMessage   = err ?? "";

            LoadUsers();
            LoadAvailableEmployees();
            return Page();
        }

        private void LoadUsers()
        {
            try
            {
                using var db = new MySqlConnection(_cs);
                UsersList = string.IsNullOrEmpty(SearchQuery)
                    ? db.Query<UserModel>("SELECT * FROM users ORDER BY id DESC").ToList()
                    : db.Query<UserModel>(
                        @"SELECT * FROM users
                          WHERE name LIKE @s OR email LIKE @s OR phone LIKE @s OR role LIKE @s
                          ORDER BY id DESC",
                        new { s = $"%{SearchQuery}%" }).ToList();
            }
            catch
            {
                ErrorMessage = "Error loading users.";
            }
        }

        private void LoadAvailableEmployees()
        {
            try
            {
                using var db = new MySqlConnection(_cs);
                AvailableEmployees = db.Query<EmployeeQuickDto>(
                    @"SELECT id          AS Id,
                             first_name  AS Name,
                             last_name   AS LastName,
                             COALESCE(email_address, '') AS Email,
                             COALESCE(phone_number,  '') AS Phone
                      FROM employees
                      WHERE employment_status = 'Active'
                      ORDER BY first_name ASC").ToList();
            }
            catch
            {
                // employees table may not exist yet on first run — silently skip
                AvailableEmployees = new();
            }
        }
    }
}
