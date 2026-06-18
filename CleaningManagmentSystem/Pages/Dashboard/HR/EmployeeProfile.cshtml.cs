using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class EmployeeProfileModel : PageModel
    {
        private readonly string _cs;

        public EmployeeProfileModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public EmployeeDto              Employee           { get; set; } = new();
        public List<AttendanceRecord>   RecentAttendance   { get; set; } = new();
        public List<LeaveRecord>        Leaves             { get; set; } = new();
        public List<PayrollRecord>      Payrolls           { get; set; } = new();
        public List<PerformanceReviewRecord> PerformanceReviews { get; set; } = new();
        public List<EmployeeDocument>   Documents          { get; set; } = new();

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        public IActionResult OnGet(int id)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            if (id <= 0)         return RedirectToPage("/Dashboard/HR/Employees");

            using var db = new MySqlConnection(_cs);

            // Load employee
            Employee = db.QueryFirstOrDefault<EmployeeDto>(
                "SELECT * FROM employees WHERE id=@Id", new { Id = id })
                ?? new EmployeeDto();

            if (Employee.Id == 0)
                return RedirectToPage("/Dashboard/HR/Employees");

            // Recent attendance (last 10)
            RecentAttendance = db.Query<AttendanceRecord>(
                @"SELECT * FROM employee_attendance
                  WHERE employee_id=@Id ORDER BY date DESC LIMIT 10",
                new { Id = id }).ToList();

            // All leave records — guard against missing approved_by column
            var hasApprCol = db.ExecuteScalar<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='employee_leaves' AND COLUMN_NAME='approved_by'");

            var leaveSql = hasApprCol > 0
                ? @"SELECT l.*,
                           (SELECT CONCAT(a.first_name,' ',a.last_name)
                            FROM employees a WHERE a.id = l.approved_by) AS approver_name
                    FROM employee_leaves l
                    WHERE l.employee_id=@Id ORDER BY l.created_at DESC"
                : @"SELECT l.*, '' AS approver_name
                    FROM employee_leaves l
                    WHERE l.employee_id=@Id ORDER BY l.created_at DESC";

            Leaves = db.Query<LeaveRecord>(leaveSql, new { Id = id }).ToList();

            // All payroll slips
            Payrolls = db.Query<PayrollRecord>(
                @"SELECT * FROM employee_payroll
                  WHERE employee_id=@Id
                  ORDER BY year DESC,
                    FIELD(month,'January','February','March','April','May','June',
                               'July','August','September','October','November','December')",
                new { Id = id }).ToList();

            // All performance reviews
            PerformanceReviews = db.Query<PerformanceReviewRecord>(
                @"SELECT pr.*, CONCAT(rv.first_name,' ',rv.last_name) AS reviewer_name
                  FROM employee_performance_reviews pr
                  LEFT JOIN employees rv ON rv.id = pr.reviewed_by
                  WHERE pr.employee_id=@Id ORDER BY pr.created_at DESC",
                new { Id = id }).ToList();

            // All education documents
            Documents = db.Query<EmployeeDocument>(
                "SELECT * FROM employee_documents WHERE employee_id=@Id ORDER BY uploaded_at DESC",
                new { Id = id }).ToList();

            return Page();
        }
    }
}
