using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class IndexModel : PageModel
    {
        private readonly string _cs;

        public IndexModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── KPI counts ───────────────────────────────────────────────────────
        public int     TotalEmployees    { get; set; }
        public int     ActiveEmployees   { get; set; }
        public int     PendingLeaves     { get; set; }
        public int     PresentToday      { get; set; }
        public int     AbsentToday       { get; set; }
        public int     HighPerformers    { get; set; }
        public decimal TotalNetPayroll   { get; set; }
        public int     PromoRecommended  { get; set; }

        // ── Recent activity ──────────────────────────────────────────────────
        public List<dynamic> RecentEmployees { get; set; } = new();
        public List<dynamic> PendingLeaveList{ get; set; } = new();

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin";

        public IActionResult OnGet()
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            using var db = new MySqlConnection(_cs);

            TotalEmployees  = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM employees");
            ActiveEmployees = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM employees WHERE employment_status='Active'");
            PendingLeaves   = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM employee_leaves WHERE approval_status='Pending'");

            var today = DateTime.Today.ToString("yyyy-MM-dd");
            PresentToday = db.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM employee_attendance WHERE date=@d AND attendance_status='Present'",
                new { d = today });
            AbsentToday = db.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM employee_attendance WHERE date=@d AND attendance_status='Absent'",
                new { d = today });

            // Current month payroll total
            var m = DateTime.Now.ToString("MMMM");
            var y = DateTime.Now.Year;
            TotalNetPayroll = db.QueryFirstOrDefault<decimal>(
                "SELECT COALESCE(SUM(net_salary),0) FROM employee_payroll WHERE month=@m AND year=@y",
                new { m, y });

            HighPerformers   = db.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM employee_performance_reviews WHERE final_rating >= 4");
            PromoRecommended = db.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM employee_performance_reviews WHERE promotion_recommendation='Yes'");

            // Recent 5 employees
            RecentEmployees = db.Query<dynamic>(
                @"SELECT id, first_name, last_name, department, position, employment_status, hire_date
                  FROM employees ORDER BY created_at DESC LIMIT 5").ToList();

            // Top 5 pending leave requests
            PendingLeaveList = db.Query<dynamic>(
                @"SELECT l.id, CONCAT(e.first_name,' ',e.last_name) AS employee_name,
                         e.department, l.leave_type, l.start_date, l.end_date, l.number_of_days
                  FROM employee_leaves l
                  JOIN employees e ON e.id = l.employee_id
                  WHERE l.approval_status = 'Pending'
                  ORDER BY l.created_at ASC LIMIT 5").ToList();

            return Page();
        }
    }
}
