using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class AttendanceRecord
    {
        public int       Id               { get; set; }
        public int       EmployeeId       { get; set; }
        public string    EmployeeName     { get; set; } = "";
        public string    Department       { get; set; } = "";
        public DateTime  Date             { get; set; }
        public TimeSpan  CheckInTime      { get; set; }
        public TimeSpan  CheckOutTime     { get; set; }
        public double    WorkingHours     { get; set; }
        public double    OvertimeHours    { get; set; }
        public string    AttendanceStatus { get; set; } = "Present";
        public string    Notes            { get; set; } = "";
    }

    public class AttendanceModel : PageModel
    {
        private readonly string _cs;

        public AttendanceModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public List<AttendanceRecord> Records      { get; set; } = new();
        public List<EmployeeDto>      AllEmployees { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public string DateFilter     { get; set; } = "";
        public int?   EmpFilter      { get; set; }
        public string StatusFilter   { get; set; } = "";

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet(string? date, int? empId, string? status)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            DateFilter   = date   ?? DateTime.Today.ToString("yyyy-MM-dd");
            EmpFilter    = empId;
            StatusFilter = status ?? "";

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

            var sql = @"SELECT a.*,
                               CONCAT(e.first_name,' ',e.last_name) AS employee_name,
                               e.department
                        FROM employee_attendance a
                        JOIN employees e ON e.id = a.employee_id
                        WHERE (@d='' OR a.date=@d)
                          AND (@eid IS NULL OR a.employee_id=@eid)
                          AND (@st='' OR a.attendance_status=@st)
                        ORDER BY a.date DESC, e.first_name ASC";

            Records = db.Query<AttendanceRecord>(sql,
                new { d = DateFilter, eid = EmpFilter, st = StatusFilter }).ToList();
        }

        // ── ADD ───────────────────────────────────────────────────────────────
        public IActionResult OnPostAdd(int EmployeeId, string Date,
            string? CheckInTime, string? CheckOutTime, string AttendanceStatus, string? Notes)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                // Calculate working hours
                double workHours  = 0;
                double overtimeHours = 0;
                if (TimeSpan.TryParse(CheckInTime, out var cin) && TimeSpan.TryParse(CheckOutTime, out var cout))
                {
                    workHours    = (cout - cin).TotalHours;
                    if (workHours < 0) workHours = 0;
                    overtimeHours = workHours > 8 ? workHours - 8 : 0;
                }

                using var db = new MySqlConnection(_cs);
                db.Execute(@"INSERT INTO employee_attendance
                    (employee_id, date, check_in_time, check_out_time,
                     working_hours, overtime_hours, attendance_status, notes, created_at)
                    VALUES (@Eid, @Dt, @Ci, @Co, @Wh, @Oh, @St, @Nt, NOW())",
                    new
                    {
                        Eid = EmployeeId, Dt = Date,
                        Ci  = CheckInTime  ?? "00:00",
                        Co  = CheckOutTime ?? "00:00",
                        Wh  = Math.Round(workHours, 2),
                        Oh  = Math.Round(overtimeHours, 2),
                        St  = AttendanceStatus,
                        Nt  = Notes ?? ""
                    });

                TempData["Success"] = "Attendance recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            return RedirectToPage();
        }

        // ── UPDATE ────────────────────────────────────────────────────────────
        public IActionResult OnPostUpdate(int Id, int EmployeeId, string Date,
            string? CheckInTime, string? CheckOutTime, string AttendanceStatus, string? Notes)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                double workHours = 0, overtimeHours = 0;
                if (TimeSpan.TryParse(CheckInTime, out var cin) && TimeSpan.TryParse(CheckOutTime, out var cout))
                {
                    workHours     = Math.Max(0, (cout - cin).TotalHours);
                    overtimeHours = workHours > 8 ? workHours - 8 : 0;
                }

                using var db = new MySqlConnection(_cs);
                db.Execute(@"UPDATE employee_attendance SET
                    employee_id=@Eid, date=@Dt, check_in_time=@Ci, check_out_time=@Co,
                    working_hours=@Wh, overtime_hours=@Oh, attendance_status=@St, notes=@Nt
                    WHERE id=@Id",
                    new
                    {
                        Id, Eid = EmployeeId, Dt = Date,
                        Ci = CheckInTime ?? "00:00", Co = CheckOutTime ?? "00:00",
                        Wh = Math.Round(workHours, 2), Oh = Math.Round(overtimeHours, 2),
                        St = AttendanceStatus, Nt = Notes ?? ""
                    });

                TempData["Success"] = "Attendance record updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Update error: {ex.Message}";
            }
            return RedirectToPage();
        }
    }
}
