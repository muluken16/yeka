using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class PayrollRecord
    {
        public int      Id           { get; set; }
        public int      EmployeeId   { get; set; }
        public string   EmployeeName { get; set; } = "";
        public string   Department   { get; set; } = "";
        public string   Month        { get; set; } = "";
        public int      Year         { get; set; }
        public decimal  BasicSalary  { get; set; }
        public decimal  Allowances   { get; set; }
        public decimal  OvertimePay  { get; set; }
        public decimal  Bonuses      { get; set; }
        public decimal  Deductions   { get; set; }
        public decimal  Tax          { get; set; }
        public decimal  NetSalary    { get; set; }
        public string   Status       { get; set; } = "Pending";
        public DateTime CreatedAt    { get; set; }
    }

    public class PayrollManagementModel : PageModel
    {
        private readonly string _cs;

        public PayrollManagementModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public List<PayrollRecord> Records      { get; set; } = new();
        public List<EmployeeDto>   AllEmployees { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public string MonthFilter    { get; set; } = "";
        public int?   YearFilter     { get; set; }
        public int?   EmpFilter      { get; set; }

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet(string? month, int? year, int? empId)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            MonthFilter = month  ?? "";
            YearFilter  = year;
            EmpFilter   = empId;

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage   = TempData["Error"]?.ToString()   ?? "";

            LoadData();
            return Page();
        }

        private void LoadData()
        {
            using var db = new MySqlConnection(_cs);

            AllEmployees = db.Query<EmployeeDto>(
                "SELECT id, first_name, last_name, department, salary FROM employees WHERE employment_status='Active' ORDER BY first_name")
                .ToList();

            var sql = @"SELECT p.*, CONCAT(e.first_name,' ',e.last_name) AS employee_name, e.department
                        FROM employee_payroll p
                        JOIN employees e ON e.id = p.employee_id
                        WHERE (@m='' OR p.month=@m)
                          AND (@y IS NULL OR p.year=@y)
                          AND (@eid IS NULL OR p.employee_id=@eid)
                        ORDER BY p.year DESC, FIELD(p.month,
                            'January','February','March','April','May','June',
                            'July','August','September','October','November','December'), e.first_name";

            Records = db.Query<PayrollRecord>(sql, new { m = MonthFilter, y = YearFilter, eid = EmpFilter })
                        .ToList();
        }

        // ── ADD ───────────────────────────────────────────────────────────────
        public IActionResult OnPostAdd(
            int EmployeeId, string Month, int Year,
            decimal BasicSalary, decimal Allowances, decimal OvertimePay,
            decimal Bonuses, decimal Deductions, decimal Tax, decimal NetSalary)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                using var db = new MySqlConnection(_cs);

                // Prevent duplicate for same employee/month/year
                var exists = db.QueryFirstOrDefault<int>(
                    "SELECT COUNT(*) FROM employee_payroll WHERE employee_id=@Eid AND month=@M AND year=@Y",
                    new { Eid = EmployeeId, M = Month, Y = Year });

                if (exists > 0)
                {
                    TempData["Error"] = $"Payroll for this employee already exists for {Month} {Year}. Delete it first to re-process.";
                    return RedirectToPage();
                }

                // Calculate net server-side for safety
                var net = BasicSalary + Allowances + OvertimePay + Bonuses - Deductions - Tax;

                db.Execute(@"INSERT INTO employee_payroll
                    (employee_id, month, year, basic_salary, allowances, overtime_pay, bonuses, deductions, tax, net_salary, status, created_at)
                    VALUES (@Eid, @M, @Y, @Bs, @Al, @Ov, @Bn, @De, @Tx, @Net, 'Pending', NOW())",
                    new
                    {
                        Eid = EmployeeId, M = Month, Y = Year,
                        Bs = BasicSalary, Al = Allowances, Ov = OvertimePay,
                        Bn = Bonuses, De = Deductions, Tx = Tax, Net = net
                    });

                var empName = db.QueryFirstOrDefault<string>(
                    "SELECT CONCAT(first_name,' ',last_name) FROM employees WHERE id=@Id", new { Id = EmployeeId });

                TempData["Success"] = $"Payroll for {empName} — {Month} {Year} saved. Net: {net:N0} ETB.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            return RedirectToPage();
        }

        // ── MARK AS PAID ──────────────────────────────────────────────────────
        public IActionResult OnPostMarkPaid(int id)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            using var db = new MySqlConnection(_cs);
            db.Execute("UPDATE employee_payroll SET status='Paid' WHERE id=@Id", new { Id = id });
            TempData["Success"] = "Payroll marked as Paid.";
            return RedirectToPage();
        }

        // ── DELETE ────────────────────────────────────────────────────────────
        public IActionResult OnPostDelete(int id)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            using var db = new MySqlConnection(_cs);
            db.Execute("DELETE FROM employee_payroll WHERE id=@Id", new { Id = id });
            TempData["Success"] = "Payroll record deleted.";
            return RedirectToPage();
        }
    }
}
