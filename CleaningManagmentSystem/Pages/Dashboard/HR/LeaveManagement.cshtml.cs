using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class LeaveRecord
    {
        public int      Id               { get; set; }
        public int      EmployeeId       { get; set; }
        public string   EmployeeName     { get; set; } = "";
        public string   Department       { get; set; } = "";
        public string   LeaveType        { get; set; } = "";
        public DateTime StartDate        { get; set; }
        public DateTime EndDate          { get; set; }
        public int      NumberOfDays     { get; set; }
        public string   Reason           { get; set; } = "";
        public string   ApprovalStatus   { get; set; } = "Pending";
        public string   ApproverName     { get; set; } = "";
        public DateTime CreatedAt        { get; set; }
    }

    public class LeaveManagementModel : PageModel
    {
        private readonly string _cs;

        public LeaveManagementModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public List<LeaveRecord>  Leaves       { get; set; } = new();
        public List<EmployeeDto>  AllEmployees { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public string StatusFilter   { get; set; } = "";
        public string TypeFilter     { get; set; } = "";

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet(string? status, string? leaveType)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            StatusFilter = status    ?? "";
            TypeFilter   = leaveType ?? "";

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage   = TempData["Error"]?.ToString()   ?? "";

            LoadData();
            return Page();
        }

        private void LoadData()
        {
            using var db = new MySqlConnection(_cs);

            AllEmployees = db.Query<EmployeeDto>(
                "SELECT id, first_name, last_name, department FROM employees WHERE employment_status='Active' ORDER BY first_name")
                .ToList();

            // Check whether approved_by column exists (may be missing on older DBs)
            var hasApprovedBy = db.ExecuteScalar<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME   = 'employee_leaves'
                    AND COLUMN_NAME  = 'approved_by'");

            string approverExpr = hasApprovedBy > 0
                ? @"COALESCE(
                       (SELECT CONCAT(a.first_name,' ',a.last_name)
                        FROM employees a
                        WHERE a.id = l.approved_by
                        LIMIT 1),
                   '') AS approver_name"
                : "'' AS approver_name";

            var sql = $@"SELECT l.id, l.employee_id, l.leave_type,
                               l.start_date, l.end_date, l.number_of_days,
                               l.reason, l.approval_status, l.created_at,
                               CONCAT(e.first_name,' ',e.last_name) AS employee_name,
                               e.department,
                               {approverExpr}
                        FROM employee_leaves l
                        JOIN employees e ON e.id = l.employee_id
                        WHERE (@st='' OR l.approval_status=@st)
                          AND (@lt='' OR l.leave_type=@lt)
                        ORDER BY l.created_at DESC";

            Leaves = db.Query<LeaveRecord>(sql, new { st = StatusFilter, lt = TypeFilter }).ToList();
        }

        // ── ADD ───────────────────────────────────────────────────────────────
        public IActionResult OnPostAdd(int EmployeeId, string LeaveType,
            string StartDate, string EndDate, int NumberOfDays, string? Reason)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute(@"INSERT INTO employee_leaves
                    (employee_id, leave_type, start_date, end_date, number_of_days, reason, approval_status, created_at)
                    VALUES (@Eid, @Lt, @Sd, @Ed, @Nd, @Rs, 'Pending', NOW())",
                    new { Eid = EmployeeId, Lt = LeaveType, Sd = StartDate, Ed = EndDate, Nd = NumberOfDays, Rs = Reason ?? "" });

                TempData["Success"] = "Leave request submitted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            return RedirectToPage();
        }

        // ── APPROVE ───────────────────────────────────────────────────────────
        public IActionResult OnPostApprove(int id)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            var approver = HttpContext.Session.GetInt32("UserId") ?? 0;
            using var db = new MySqlConnection(_cs);

            var hasCol = db.ExecuteScalar<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='employee_leaves' AND COLUMN_NAME='approved_by'");

            if (hasCol > 0)
                db.Execute(@"UPDATE employee_leaves
                             SET approval_status='Approved', approved_by=@ApprId, approved_at=NOW()
                             WHERE id=@Id", new { ApprId = approver, Id = id });
            else
                db.Execute("UPDATE employee_leaves SET approval_status='Approved' WHERE id=@Id", new { Id = id });

            TempData["Success"] = "Leave request approved.";
            return RedirectToPage();
        }

        // ── REJECT ────────────────────────────────────────────────────────────
        public IActionResult OnPostReject(int id)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            var approver = HttpContext.Session.GetInt32("UserId") ?? 0;
            using var db = new MySqlConnection(_cs);

            var hasCol = db.ExecuteScalar<int>(
                @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='employee_leaves' AND COLUMN_NAME='approved_by'");

            if (hasCol > 0)
                db.Execute(@"UPDATE employee_leaves
                             SET approval_status='Rejected', approved_by=@ApprId, approved_at=NOW()
                             WHERE id=@Id", new { ApprId = approver, Id = id });
            else
                db.Execute("UPDATE employee_leaves SET approval_status='Rejected' WHERE id=@Id", new { Id = id });

            TempData["Success"] = "Leave request rejected.";
            return RedirectToPage();
        }
    }
}
