using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Models;

namespace CleaningManagmentSystem.Pages.Dashboard.SuperAdmin
{
    public class RoleUsageModel : PageModel
    {
        private readonly string _cs;

        public RoleUsageModel(IConfiguration configuration)
            => _cs = configuration.GetConnectionString("DefaultConnection") ?? "";

        // ── Bound form fields (Edit modal) ──────────────────────────────────
        [BindProperty] public int?   EditId                  { get; set; }
        [BindProperty] public string RoleName                { get; set; } = "";
        [BindProperty] public string DisplayName             { get; set; } = "";
        [BindProperty] public string Description             { get; set; } = "";
        [BindProperty] public string UsageContext            { get; set; } = "";
        [BindProperty] public string PrimaryResponsibilities { get; set; } = "";
        [BindProperty] public string DailyActivities         { get; set; } = "";
        [BindProperty] public string ReportsAccess           { get; set; } = "";
        [BindProperty] public string ModulesAccess           { get; set; } = "";
        [BindProperty] public int    AccessLevel             { get; set; }
        [BindProperty] public bool   CanCreateUsers          { get; set; }
        [BindProperty] public bool   CanViewFinancials       { get; set; }
        [BindProperty] public bool   CanManageDispatch       { get; set; }
        [BindProperty] public bool   CanViewPayroll          { get; set; }
        [BindProperty] public bool   CanManageStaff          { get; set; }

        // ── Page data ────────────────────────────────────────────────────────
        public IList<RoleDefinition>   RoleDefinitions  { get; set; } = new List<RoleDefinition>();
        public IList<RoleUserStat>     RoleUserStats    { get; set; } = new List<RoleUserStat>();
        public IList<UserRoleRow>      AllUsers         { get; set; } = new List<UserRoleRow>();
        public IList<RecentUserRow>    RecentUsers      { get; set; } = new List<RecentUserRow>();

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage   { get; set; }
        public string  FilterRole     { get; set; } = "";

        // ── DTOs ─────────────────────────────────────────────────────────────
        public class RoleUserStat
        {
            public string Role        { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int    Total       { get; set; }
            public int    Active      { get; set; }
            public int    Inactive    { get; set; }
            public string BadgeColor  { get; set; } = "";
            public RoleUserStat() {}
            public RoleUserStat(string role, string displayName, int total, int active, int inactive, string badgeColor)
            { Role=role; DisplayName=displayName; Total=total; Active=active; Inactive=inactive; BadgeColor=badgeColor; }
        }

        // Dapper maps snake_case columns → properties by name (case-insensitive)
        public class UserRoleRow
        {
            public int      Id        { get; set; }
            public string   Name      { get; set; } = "";
            public string   Email     { get; set; } = "";
            public string   Role      { get; set; } = "";
            public string   Phone     { get; set; } = "";
            public bool     IsActive  { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class RecentUserRow
        {
            public int      Id        { get; set; }
            public string   Name      { get; set; } = "";
            public string   Email     { get; set; } = "";
            public string   Role      { get; set; } = "";
            public bool     IsActive  { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        // ── GET ──────────────────────────────────────────────────────────────
        public IActionResult OnGet([FromQuery] string? filterRole = null)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            FilterRole = filterRole ?? "";
            LoadAll();
            return Page();
        }

        // ── POST: Save edited role definition ────────────────────────────────
        public IActionResult OnPostSave()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                ErrorMessage = "Display Name is required.";
                LoadAll();
                return Page();
            }

            try
            {
                using var db = new MySqlConnection(_cs);
                if (EditId.HasValue)
                {
                    db.Execute(@"
                        UPDATE role_definitions SET
                            display_name             = @DisplayName,
                            description              = @Description,
                            usage_context            = @UsageContext,
                            primary_responsibilities = @PrimaryResponsibilities,
                            daily_activities         = @DailyActivities,
                            reports_access           = @ReportsAccess,
                            modules_access           = @ModulesAccess,
                            access_level             = @AccessLevel,
                            can_create_users         = @CanCreateUsers,
                            can_view_financials      = @CanViewFinancials,
                            can_manage_dispatch      = @CanManageDispatch,
                            can_view_payroll         = @CanViewPayroll,
                            can_manage_staff         = @CanManageStaff,
                            updated_at               = NOW()
                        WHERE id = @EditId",
                        new { EditId, DisplayName, Description, UsageContext,
                              PrimaryResponsibilities, DailyActivities,
                              ReportsAccess, ModulesAccess, AccessLevel,
                              CanCreateUsers, CanViewFinancials, CanManageDispatch,
                              CanViewPayroll, CanManageStaff });

                    SuccessMessage = $"Role '{DisplayName}' updated successfully.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error saving role: {ex.Message}";
            }

            LoadAll();
            return Page();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void LoadAll()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                // Role definitions table
                RoleDefinitions = db.Query<RoleDefinition>(
                    "SELECT * FROM role_definitions ORDER BY access_level DESC, display_name ASC")
                    .ToList();

                // Per-role user counts  (uses the live users table)
                var rawStats = db.Query(
                    @"SELECT LOWER(COALESCE(role,'')) AS role,
                             COUNT(*)                 AS total,
                             SUM(is_active)           AS active
                      FROM users
                      GROUP BY LOWER(COALESCE(role,''))
                      ORDER BY total DESC")
                    .ToList();

                RoleUserStats = rawStats.Select(r =>
                {
                    string rol  = (string)(r.role  ?? "");
                    int    tot  = (int)(long)(r.total  ?? 0L);
                    int    act  = (int)(long)(r.active ?? 0L);
                    string disp = RoleDefinitions.FirstOrDefault(
                                    d => d.RoleName.Equals(rol, StringComparison.OrdinalIgnoreCase))
                                  ?.DisplayName ?? FallbackDisplay(rol);
                    return new RoleUserStat(rol, disp, tot, act, tot - act, RoleBadge(rol));
                }).ToList();

                // All users (filtered if a role is selected)
                var userSql = string.IsNullOrEmpty(FilterRole)
                    ? @"SELECT id, name, email, COALESCE(role,'') AS role,
                               COALESCE(phone,'') AS phone, is_active, created_at
                         FROM users ORDER BY role, name"
                    : @"SELECT id, name, email, COALESCE(role,'') AS role,
                               COALESCE(phone,'') AS phone, is_active, created_at
                         FROM users
                         WHERE LOWER(COALESCE(role,'')) = LOWER(@r)
                         ORDER BY name";

                AllUsers = db.Query<UserRoleRow>(userSql, new { r = FilterRole }).ToList();

                // 10 most recently created users
                RecentUsers = db.Query<RecentUserRow>(
                    @"SELECT id, name, email, COALESCE(role,'') AS role,
                             is_active, created_at
                      FROM users ORDER BY created_at DESC LIMIT 10")
                    .ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading data: {ex.Message}";
            }
        }

        // ── Static helpers ────────────────────────────────────────────────────
        public static string RoleBadge(string role) => role.ToLower() switch
        {
            "superadmin"        => "danger",
            "hr"                => "purple",
            "manager"           => "primary",
            "driver"            => "success",
            "outsource"         => "warning",
            "privatecompanyrep" => "info",
            "staff"             => "secondary",
            "wereda_mahberat"   => "dark",
            "dispatchofficer"   => "indigo",
            _                   => "secondary"
        };

        public static string FallbackDisplay(string role) => role.ToLower() switch
        {
            "superadmin"        => "Super Admin",
            "hr"                => "Human Resources",
            "manager"           => "Manager",
            "driver"            => "Driver",
            "outsource"         => "Outsource",
            "privatecompanyrep" => "Private Company Rep",
            "staff"             => "Staff",
            "wereda_mahberat"   => "Wereda Mahberat",
            "dispatchofficer"   => "Dispatch Officer",
            ""                  => "⚠ No Role",
            _                   => role
        };
    }
}
