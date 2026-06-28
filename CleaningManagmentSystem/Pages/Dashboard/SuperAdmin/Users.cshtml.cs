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
        public string RoleFilter     { get; set; } = "";
        public List<dynamic> UnlinkedCompanies { get; set; } = new();

        public static readonly HashSet<string> MobileRoles = new(StringComparer.OrdinalIgnoreCase)
            { "driver", "outsource", "PrivateCompanyRep", "manager" };

        public UsersModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // GET only — all mutations go through /admin/users/* controller actions
        public IActionResult OnGet(
            [FromQuery] string? searchQuery = null,
            [FromQuery] string? roleFilter  = null,
            [FromQuery] string? ok = null,
            [FromQuery] string? err = null)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            SearchQuery    = searchQuery ?? "";
            RoleFilter     = roleFilter  ?? "";
            SuccessMessage = ok  ?? "";
            ErrorMessage   = err ?? "";

            LoadUsers();
            return Page();
        }

        private void LoadUsers()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                var conditions = new List<string>();
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    conditions.Add("(name LIKE @s OR email LIKE @s OR phone LIKE @s)");
                    parameters.Add("s", $"%{SearchQuery}%");
                }
                if (!string.IsNullOrEmpty(RoleFilter))
                {
                    conditions.Add("role = @r");
                    parameters.Add("r", RoleFilter);
                }

                var where = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
                UsersList = db.Query<UserModel>(
                    $"SELECT * FROM users {where} ORDER BY id DESC", parameters).ToList();

                UnlinkedCompanies = db.Query("SELECT id, company_name, email, contact_person, phone FROM private_cleaning_companies WHERE rep_user_id IS NULL OR rep_user_id = 0 ORDER BY company_name").ToList();
            }
            catch
            {
                ErrorMessage = "Error loading users.";
            }
        }
    }
}
