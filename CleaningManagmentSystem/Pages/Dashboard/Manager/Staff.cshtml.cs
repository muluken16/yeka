using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Manager
{
    // Read-only view of employees for the Manager role.
    // Shows: name, department, position, job_grade, employment_type,
    //        employment_status, phone_number, email_address, hire_date, work_location.
    // Does NOT expose salary, bank account, tax ID, pension ID, or other sensitive HR data.
    public class StaffModel : PageModel
    {
        private readonly string _cs;

        public StaffModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public List<StaffEmployeeRow> Employees    { get; set; } = new();
        public string Search       { get; set; } = "";
        public string DeptFilter   { get; set; } = "";
        public string StatusFilter { get; set; } = "";
        public List<string> Departments { get; set; } = new();

        // Stats
        public int TotalCount    { get; set; }
        public int ActiveCount   { get; set; }
        public int InactiveCount { get; set; }
        public int DeptCount     { get; set; }

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "manager" or "hr" or "superadmin";

        public IActionResult OnGet(string? search, string? dept, string? status)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            Search       = search ?? "";
            DeptFilter   = dept   ?? "";
            StatusFilter = status ?? "";

            Load();
            return Page();
        }

        private void Load()
        {
            using var db = new MySqlConnection(_cs);

            // All distinct departments for filter
            Departments = db.Query<string>(
                "SELECT DISTINCT department FROM employees WHERE department != '' ORDER BY department")
                .ToList();

            var sql = @"
                SELECT id, employee_code, first_name, last_name, middle_name,
                       department, position, job_grade, employment_type,
                       employment_status, phone_number, email_address,
                       hire_date, work_location, photo_url, gender,
                       COALESCE(user_id, 0) AS user_id
                FROM employees
                WHERE 1=1
                  AND (@s = '' OR CONCAT(first_name,' ',last_name,' ',department,' ',position) LIKE @like)
                  AND (@d = '' OR department = @d)
                  AND (@st = '' OR employment_status = @st)
                ORDER BY first_name ASC";

            Employees = db.Query<StaffEmployeeRow>(sql, new
            {
                s    = Search,
                like = $"%{Search}%",
                d    = DeptFilter,
                st   = StatusFilter
            }).ToList();

            TotalCount    = Employees.Count;
            ActiveCount   = Employees.Count(e => e.employment_status == "Active");
            InactiveCount = Employees.Count(e => e.employment_status != "Active");
            DeptCount     = Employees.Select(e => e.department)
                                     .Where(d => !string.IsNullOrEmpty(d))
                                     .Distinct().Count();
        }
    }

    public class StaffEmployeeRow
    {
        public int      id                { get; set; }
        public string   employee_code     { get; set; } = "";
        public string   first_name        { get; set; } = "";
        public string   last_name         { get; set; } = "";
        public string   middle_name       { get; set; } = "";
        public string   department        { get; set; } = "";
        public string   position          { get; set; } = "";
        public string   job_grade         { get; set; } = "";
        public string   employment_type   { get; set; } = "";
        public string   employment_status { get; set; } = "";
        public string   phone_number      { get; set; } = "";
        public string   email_address     { get; set; } = "";
        public DateTime hire_date         { get; set; }
        public string   work_location     { get; set; } = "";
        public string   photo_url         { get; set; } = "";
        public string   gender            { get; set; } = "";
        public int      user_id           { get; set; }   // 0 = no linked user account

        public string FullName  => $"{first_name} {last_name}".Trim();
        public string Initials  => (first_name.Length > 0 ? first_name[0].ToString() : "")
                                 + (last_name.Length  > 0 ? last_name[0].ToString()  : "");
        public int YearsService => (DateTime.Today - hire_date).Days / 365;
    }
}
