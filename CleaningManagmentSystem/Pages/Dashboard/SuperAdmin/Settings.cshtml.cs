using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.SuperAdmin
{
    public class SettingsModel : PageModel
    {
        private readonly string _cs;
        private readonly IConfiguration _cfg;

        public SettingsModel(IConfiguration cfg)
        {
            _cfg = cfg;
            _cs  = cfg.GetConnectionString("DefaultConnection") ?? "";
        }

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        // ── System info ──────────────────────────────────────────────────────
        public string AppVersion    { get; set; } = "1.0.0";
        public string DbVersion     { get; set; } = "";
        public string DbHost        { get; set; } = "";
        public string DbName        { get; set; } = "";
        public string Environment   { get; set; } = "";

        // ── Live counters ────────────────────────────────────────────────────
        public int     TotalUsers          { get; set; }
        public int     ActiveUsers         { get; set; }
        public int     InactiveUsers       { get; set; }
        public int     TotalEmployees      { get; set; }
        public int     ActiveEmployees     { get; set; }
        public int     OutsourceCompanies  { get; set; }
        public int     ActiveOutsource     { get; set; }
        public int     PrivateCompanies    { get; set; }
        public int     ActivePrivate       { get; set; }
        public int     TotalReceipts       { get; set; }
        public decimal TotalReceiptAmount  { get; set; }
        public int     TotalPayroll        { get; set; }
        public decimal TotalPayrollPaid    { get; set; }
        public decimal TotalCapital        { get; set; }
        public decimal TotalIncome         { get; set; }
        public decimal TotalExpense        { get; set; }
        public int     TotalRolesDefined   { get; set; }
        public int     TotalPosts          { get; set; }
        public int     TotalTrainings      { get; set; }
        public int     TotalTransports     { get; set; }

        // ── Role breakdown ───────────────────────────────────────────────────
        public List<RoleBreakdown> RoleStats { get; set; } = new();

        // ── Recent activity ──────────────────────────────────────────────────
        public List<RecentUser>    RecentUsers    { get; set; } = new();
        public List<TableSizeRow>  TableSizes     { get; set; } = new();

        public class RoleBreakdown
        {
            public string Role   { get; set; } = "";
            public int    Total  { get; set; }
            public long   Active { get; set; }  // SUM() returns long in MariaDB
            public int    Inactive => (int)(Total - Active);
        }
        public class RecentUser
        {
            public int      Id        { get; set; }
            public string   Name      { get; set; } = "";
            public string   Email     { get; set; } = "";
            public string   Role      { get; set; } = "";
            public bool     IsActive  { get; set; }
            public DateTime CreatedAt { get; set; }
        }
        public class TableSizeRow
        {
            public string TableName { get; set; } = "";
            public long   Rows      { get; set; }
        }

        // ── GET ──────────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            ParseConnectionString();
            Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            LoadAll();
            return Page();
        }

        // ── POST: clear session cache ─────────────────────────────────────────
        public IActionResult OnPostResetSession()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            var user = HttpContext.Session.GetString("UserName");
            var role = HttpContext.Session.GetString("UserRole");
            var uid  = HttpContext.Session.GetInt32("UserId");
            HttpContext.Session.Clear();
            HttpContext.Session.SetString("UserName", user ?? "");
            HttpContext.Session.SetString("UserRole", role ?? "");
            if (uid.HasValue) HttpContext.Session.SetInt32("UserId", uid.Value);

            SuccessMessage = "Session cache cleared. Your login session is preserved.";
            ParseConnectionString();
            Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            LoadAll();
            return Page();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void ParseConnectionString()
        {
            // Extract host and database name from connection string for display
            try
            {
                var parts = _cs.Split(';');
                foreach (var p in parts)
                {
                    var kv = p.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var k = kv[0].Trim().ToLower();
                    var v = kv[1].Trim();
                    if (k is "server" or "host" or "data source")   DbHost = v;
                    if (k is "database" or "initial catalog")        DbName = v;
                }
            }
            catch { }
        }

        private void LoadAll()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                DbVersion = db.QuerySingleOrDefault<string>("SELECT VERSION()") ?? "Unknown";

                // Users
                TotalUsers    = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM users");
                ActiveUsers   = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM users WHERE is_active=1");
                InactiveUsers = TotalUsers - ActiveUsers;

                // Role breakdown
                RoleStats = db.Query<RoleBreakdown>(
                    @"SELECT COALESCE(NULLIF(role,''),'(no role)') AS Role,
                             COUNT(*) AS Total,
                             SUM(is_active) AS Active
                      FROM users
                      GROUP BY COALESCE(NULLIF(role,''),'(no role)')
                      ORDER BY Total DESC").ToList();

                // Employees
                TotalEmployees  = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM employees");
                ActiveEmployees = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM employees WHERE employment_status='Active'");

                // Companies
                OutsourceCompanies = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM outsource_companies");
                ActiveOutsource    = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM outsource_companies WHERE status='Active'");
                PrivateCompanies   = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM private_cleaning_companies");
                ActivePrivate      = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM private_cleaning_companies WHERE is_active=1");

                // Finance
                TotalReceipts      = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM receipts");
                TotalReceiptAmount = db.QuerySingleOrDefault<decimal?>("SELECT COALESCE(SUM(amount),0) FROM receipts") ?? 0m;
                TotalPayroll       = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM payroll WHERE status!='Cancelled'");
                TotalPayrollPaid   = db.QuerySingleOrDefault<decimal?>("SELECT COALESCE(SUM(net_salary),0) FROM payroll WHERE status='Paid'") ?? 0m;
                TotalIncome        = db.QuerySingleOrDefault<decimal?>("SELECT COALESCE(SUM(amount),0) FROM capital_transactions WHERE transaction_type='Income'") ?? 0m;
                TotalExpense       = db.QuerySingleOrDefault<decimal?>("SELECT COALESCE(SUM(amount),0) FROM capital_transactions WHERE transaction_type='Expense'") ?? 0m;
                TotalCapital       = TotalIncome - TotalExpense;

                // Misc
                TotalRolesDefined = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM role_definitions");
                TotalPosts        = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM posts");
                TotalTrainings    = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM trainings");
                TotalTransports   = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM transport_requests");

                // Recent users (last 8)
                RecentUsers = db.Query<RecentUser>(
                    @"SELECT id, name, email,
                             COALESCE(role,'') AS role,
                             is_active, created_at
                      FROM users ORDER BY id DESC LIMIT 8").ToList();

                // Table row counts (key tables)
                TableSizes = db.Query<TableSizeRow>(
                    @"SELECT table_name AS TableName,
                             table_rows AS `Rows`
                      FROM information_schema.tables
                      WHERE table_schema = DATABASE()
                        AND table_name IN ('users','employees','outsource_companies',
                                          'private_cleaning_companies','receipts',
                                          'payroll','capital_transactions',
                                          'transport_requests','trainings','posts',
                                          'role_definitions','employee_leaves')
                      ORDER BY table_rows DESC").ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading data: {ex.Message}";
            }
        }
    }
}
