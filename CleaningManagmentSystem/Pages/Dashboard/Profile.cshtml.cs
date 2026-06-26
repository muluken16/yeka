using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard
{
    /// <summary>
    /// Universal profile page — access rules:
    ///   HR         → can view/edit ANY user's profile via ?userId=X  (full details)
    ///   Manager    → can view ANY user's profile via ?userId=X        (read-only, no salary/bank)
    ///   Everyone else → can only see their OWN profile                (own data only)
    /// </summary>
    public class ProfileModel : PageModel
    {
        private readonly string _cs;

        public ProfileModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── Viewing mode ──────────────────────────────────────────────────
        public enum ViewMode { OwnProfile, HrManage, ManagerView }
        public ViewMode Mode { get; set; } = ViewMode.OwnProfile;
        public bool IsOwnProfile  => Mode == ViewMode.OwnProfile;
        public bool IsHrManage    => Mode == ViewMode.HrManage;
        public bool IsManagerView => Mode == ViewMode.ManagerView;
        // HR sees salary/bank; Manager and self do NOT see salary unless it's their own and they're HR
        public bool ShowSensitive => Mode == ViewMode.HrManage;

        // ── Viewed user data (may differ from logged-in user) ─────────────
        public int    UserId      { get; set; }
        public string UserName    { get; set; } = "";
        public string UserEmail   { get; set; } = "";
        public string UserRole    { get; set; } = "";
        public string UserPhone   { get; set; } = "";
        public DateTime UserCreatedAt { get; set; }

        // ── Logged-in user ────────────────────────────────────────────────
        public int    SessionUserId   { get; set; }
        public string SessionUserRole { get; set; } = "";

        // ── Linked employee record ────────────────────────────────────────
        public EmployeeProfileRecord? Employee { get; set; }

        // ── All employees list (for HR to link accounts) ──────────────────
        public List<EmpSelectItem> AllEmployees { get; set; } = new();

        // ── Form binding ──────────────────────────────────────────────────
        [BindProperty] public string NewName    { get; set; } = "";
        [BindProperty] public string NewPhone   { get; set; } = "";
        [BindProperty] public string CurrPwd    { get; set; } = "";
        [BindProperty] public string NewPwd     { get; set; } = "";
        [BindProperty] public string ConfirmPwd { get; set; } = "";

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        // ── GET ───────────────────────────────────────────────────────────
        public IActionResult OnGet(int? userId)
        {
            var sid   = HttpContext.Session.GetInt32("UserId");
            var srole = HttpContext.Session.GetString("UserRole") ?? "";
            if (sid == null) return RedirectToPage("/Login");

            SessionUserId   = sid.Value;
            SessionUserRole = srole;

            // Determine who we're viewing
            int targetId = sid.Value; // default: own profile
            if (userId.HasValue && userId.Value != sid.Value)
            {
                // Only HR and Manager can view other users
                if (srole is "hr" or "superadmin")
                    { Mode = ViewMode.HrManage; targetId = userId.Value; }
                else if (srole == "manager")
                    { Mode = ViewMode.ManagerView; targetId = userId.Value; }
                else
                    return RedirectToPage("/Dashboard/Profile"); // strip param — own only
            }

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage   = TempData["Error"]?.ToString()   ?? "";
            Load(targetId, srole);
            return Page();
        }

        // ── Update name/phone — only own profile or HR ────────────────────
        public IActionResult OnPostUpdateProfile(int targetUserId)
        {
            var sid   = HttpContext.Session.GetInt32("UserId");
            var srole = HttpContext.Session.GetString("UserRole") ?? "";
            if (sid == null) return RedirectToPage("/Login");

            // Only HR can edit others; everyone can edit themselves
            int editId = targetUserId > 0 && srole is "hr" or "superadmin"
                ? targetUserId : sid.Value;

            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute("UPDATE users SET name=@N, phone=@P, updated_at=NOW() WHERE id=@Id",
                    new { N = NewName.Trim(), P = NewPhone.Trim(), Id = editId });
                if (editId == sid.Value)
                {
                    HttpContext.Session.SetString("UserName", NewName.Trim());
                    // Refresh photo in session too
                    var photo = db.QueryFirstOrDefault<string>(
                        "SELECT COALESCE(photo_url,'') FROM employees WHERE user_id=@uid",
                        new { uid = editId });
                    HttpContext.Session.SetString("UserPhoto", photo ?? "");
                }
                TempData["Success"] = "Profile updated.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return editId == sid.Value
                ? RedirectToPage()
                : RedirectToPage(new { userId = editId });
        }

        // ── Change password — only own profile ────────────────────────────
        public IActionResult OnPostChangePassword()
        {
            var sid = HttpContext.Session.GetInt32("UserId");
            if (sid == null) return RedirectToPage("/Login");

            try
            {
                if (string.IsNullOrWhiteSpace(NewPwd) || NewPwd.Length < 6)
                    throw new Exception("New password must be at least 6 characters.");
                if (NewPwd != ConfirmPwd)
                    throw new Exception("Passwords do not match.");

                using var db = new MySqlConnection(_cs);
                var stored = db.QueryFirstOrDefault<string>(
                    "SELECT password FROM users WHERE id=@Id", new { Id = sid });
                if (stored != CurrPwd)
                    throw new Exception("Current password is incorrect.");

                db.Execute("UPDATE users SET password=@P, updated_at=NOW() WHERE id=@Id",
                    new { P = NewPwd, Id = sid });
                TempData["Success"] = "Password changed.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToPage();
        }

        // ── HR: link a user account to an employee record ─────────────────
        public IActionResult OnPostLinkEmployee(int targetUserId, int employeeId)
        {
            var srole = HttpContext.Session.GetString("UserRole") ?? "";
            var sid   = HttpContext.Session.GetInt32("UserId");
            if (sid == null) return RedirectToPage("/Login");
            if (srole is not ("hr" or "superadmin"))
                return RedirectToPage();

            int linkForUser = targetUserId > 0 ? targetUserId : sid.Value;

            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute("UPDATE employees SET user_id=NULL WHERE user_id=@Uid",
                    new { Uid = linkForUser });
                if (employeeId > 0)
                    db.Execute("UPDATE employees SET user_id=@Uid WHERE id=@EId",
                        new { Uid = linkForUser, EId = employeeId });
                TempData["Success"] = employeeId > 0
                    ? "Employee record linked successfully."
                    : "Employee link removed.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return linkForUser == sid.Value
                ? RedirectToPage()
                : RedirectToPage(new { userId = linkForUser });
        }

        // ── Load data ─────────────────────────────────────────────────────
        private void Load(int targetId, string sessionRole)
        {
            using var db = new MySqlConnection(_cs);

            var u = db.QueryFirstOrDefault<dynamic>(
                "SELECT id, name, email, role, phone, created_at FROM users WHERE id=@Id",
                new { Id = targetId });
            if (u != null)
            {
                UserId        = (int)u.id;
                UserName      = u.name  ?? "";
                UserEmail     = u.email ?? "";
                UserRole      = u.role  ?? "";
                UserPhone     = u.phone ?? "";
                UserCreatedAt = u.created_at;
            }
            NewName  = UserName;
            NewPhone = UserPhone;

            // Build employee SELECT — HR gets salary/bank; Manager and self get public fields only
            var empSql = ShowSensitive
                ? @"SELECT id, employee_code, first_name, last_name, middle_name,
                           department, position, job_grade, employment_type, employment_status,
                           hire_date, work_location, phone_number, email_address,
                           photo_url, gender, date_of_birth, nationality, marital_status,
                           highest_education, field_of_study, institution, graduation_year,
                           salary, bank_name, bank_account, tax_id, pension_id,
                           national_id, blood_type, disability_status, skills, notes
                    FROM employees WHERE user_id=@Uid"
                : @"SELECT id, employee_code, first_name, last_name, middle_name,
                           department, position, job_grade, employment_type, employment_status,
                           hire_date, work_location, phone_number, email_address,
                           photo_url, gender, date_of_birth, nationality, marital_status,
                           highest_education, field_of_study, institution, graduation_year,
                           0 AS salary, '' AS bank_name, '' AS bank_account,
                           '' AS tax_id, '' AS pension_id,
                           national_id, blood_type, disability_status, skills, notes
                    FROM employees WHERE user_id=@Uid";

            Employee = db.QueryFirstOrDefault<EmployeeProfileRecord>(empSql, new { Uid = targetId });

            // HR: load all employees for the link dropdown
            if (sessionRole is "hr" or "superadmin")
            {
                AllEmployees = db.Query<EmpSelectItem>(
                    @"SELECT id, employee_code, first_name, last_name, department, user_id
                      FROM employees ORDER BY first_name")
                    .ToList();
            }
        }
    }

    public class EmpSelectItem
    {
        public int     Id           { get; set; }
        public string  EmployeeCode { get; set; } = "";
        public string  FirstName    { get; set; } = "";
        public string  LastName     { get; set; } = "";
        public string  Department   { get; set; } = "";
        public int?    UserId       { get; set; }
        public string  FullName     => $"{FirstName} {LastName}".Trim();
        public bool    IsLinked     => UserId.HasValue && UserId > 0;
    }

    public class EmployeeProfileRecord
    {
        public int      Id               { get; set; }
        public string   EmployeeCode     { get; set; } = "";
        public string   FirstName        { get; set; } = "";
        public string   LastName         { get; set; } = "";
        public string   MiddleName       { get; set; } = "";
        public string   Department       { get; set; } = "";
        public string   Position         { get; set; } = "";
        public string   JobGrade         { get; set; } = "";
        public string   EmploymentType   { get; set; } = "";
        public string   EmploymentStatus { get; set; } = "";
        public DateTime HireDate         { get; set; }
        public string   WorkLocation     { get; set; } = "";
        public string   PhoneNumber      { get; set; } = "";
        public string   EmailAddress     { get; set; } = "";
        public string   PhotoUrl         { get; set; } = "";
        public string   Gender           { get; set; } = "";
        public DateTime? DateOfBirth     { get; set; }
        public string   Nationality      { get; set; } = "";
        public string   MaritalStatus    { get; set; } = "";
        public string   HighestEducation { get; set; } = "";
        public string   FieldOfStudy     { get; set; } = "";
        public string   Institution      { get; set; } = "";
        public int?     GraduationYear   { get; set; }
        public decimal? Salary           { get; set; }
        public string   BankName         { get; set; } = "";
        public string   BankAccount      { get; set; } = "";
        public string   TaxId            { get; set; } = "";
        public string   PensionId        { get; set; } = "";
        public string   NationalId       { get; set; } = "";
        public string   BloodType        { get; set; } = "";
        public string   DisabilityStatus { get; set; } = "";
        public string   Skills           { get; set; } = "";
        public string   Notes            { get; set; } = "";

        public string FullName    => $"{FirstName} {LastName}".Trim();
        public string Initials    => (FirstName.Length > 0 ? FirstName[0].ToString() : "")
                                   + (LastName.Length  > 0 ? LastName[0].ToString()  : "");
        public int    YearsService => HireDate > DateTime.MinValue
                                   ? (DateTime.Today - HireDate).Days / 365 : 0;
    }
}
