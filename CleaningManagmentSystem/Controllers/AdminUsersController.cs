using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Models;

namespace CleaningManagmentSystem.Controllers
{
    /// <summary>
    /// Handles all user CRUD for the SuperAdmin Users page.
    /// Route: /admin/users/*
    /// </summary>
    [Route("admin/users")]
    public class AdminUsersController : Controller
    {
        private readonly string _cs;

        public AdminUsersController(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        private IActionResult RequireAdmin()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return Redirect("/Login");
            return null!;
        }

        // ── POST /admin/users/create ────────────────────────────────────────
        [HttpPost("create")]
        public IActionResult Create(
            [FromForm] string Name,
            [FromForm] string Email,
            [FromForm] string Password,
            [FromForm] string Role,
            [FromForm] string? Phone,
            [FromForm] string? Address,
            [FromForm] int? EmployeeId,
            [FromForm] bool IsActive = true)
        {
            var check = RequireAdmin(); if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Name))     return Redirect("/Dashboard/SuperAdmin/Users?err=Name+is+required");
            if (string.IsNullOrWhiteSpace(Email))    return Redirect("/Dashboard/SuperAdmin/Users?err=Email+is+required");
            if (string.IsNullOrWhiteSpace(Password)) return Redirect("/Dashboard/SuperAdmin/Users?err=Password+is+required");
            if (string.IsNullOrWhiteSpace(Role))     return Redirect("/Dashboard/SuperAdmin/Users?err=Please+select+a+Role");

            var normalizedRole = NormalizeRole(Role);

            try
            {
                using var db = new MySqlConnection(_cs);

                // ── Check 1: duplicate email ───────────────────────────────
                var emailExists = db.QueryFirstOrDefault<int>(
                    "SELECT COUNT(*) FROM users WHERE email=@e", new { e = Email });
                if (emailExists > 0)
                    return Redirect($"/Dashboard/SuperAdmin/Users?err={Uri.EscapeDataString($"Email '{Email}' is already registered to another account.")}");

                // ── Check 2: employee already has a linked user account ────
                if (EmployeeId.HasValue && EmployeeId.Value > 0)
                {
                    var existingUserId = db.QueryFirstOrDefault<int?>(
                        "SELECT user_id FROM employees WHERE id=@eid", new { eid = EmployeeId.Value });
                    if (existingUserId.HasValue && existingUserId.Value > 0)
                    {
                        var existingUser = db.QueryFirstOrDefault<dynamic>(
                            "SELECT name, email FROM users WHERE id=@uid", new { uid = existingUserId.Value });
                        var existingName  = (string?)existingUser?.name  ?? "—";
                        var existingEmail = (string?)existingUser?.email ?? "—";
                        return Redirect($"/Dashboard/SuperAdmin/Users?err={Uri.EscapeDataString($"This employee already has an account: '{existingName}' ({existingEmail}). No new account was created.")}");
                    }
                }

                var adminId = HttpContext.Session.GetInt32("UserId") ?? 0;
                db.Execute(
                    @"INSERT INTO users (name,email,password,role,phone,address,is_active,created_by,created_at,updated_at)
                      VALUES (@n,@e,@pw,@r,@ph,@ad,@act,@cb,NOW(),NOW())",
                    new { n = Name, e = Email, pw = Password, r = normalizedRole,
                          ph = Phone ?? "", ad = Address ?? "",
                          act = IsActive, cb = adminId });

                // ── Link the new user back to the employee record ──────────
                if (EmployeeId.HasValue && EmployeeId.Value > 0)
                {
                    var newUserId = db.QueryFirst<long>("SELECT LAST_INSERT_ID()");
                    db.Execute(
                        "UPDATE employees SET user_id=@uid WHERE id=@eid",
                        new { uid = (int)newUserId, eid = EmployeeId.Value });
                }

                return Redirect($"/Dashboard/SuperAdmin/Users?ok={Uri.EscapeDataString($"User '{Name}' created with role '{normalizedRole}'")}");
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Contains("Duplicate") ? $"Email '{Email}' already exists" : ex.Message;
                return Redirect($"/Dashboard/SuperAdmin/Users?err={Uri.EscapeDataString(msg)}");
            }
        }

        // ── POST /admin/users/create-private ───────────────────────────────
        [HttpPost("create-private")]
        public IActionResult CreatePrivate(
            [FromForm] string Name,
            [FromForm] string Email,
            [FromForm] string Password,
            [FromForm] string? Phone,
            [FromForm] string? Address,
            [FromForm] string CompanyName,
            [FromForm] string? LicenseNumber,
            [FromForm] string? CompanyAddress,
            [FromForm] string? ServicesOffered)
        {
            var check = RequireAdmin(); if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(CompanyName))
                return Redirect("/Dashboard/SuperAdmin/Users?err=Name+Email+Password+and+CompanyName+are+required");

            try
            {
                using var db = new MySqlConnection(_cs);
                var adminId = HttpContext.Session.GetInt32("UserId") ?? 0;

                db.Execute(
                    @"INSERT INTO users (name,email,password,role,phone,address,is_active,created_by,created_at,updated_at)
                      VALUES (@n,@e,@pw,'PrivateCompanyRep',@ph,@ad,1,@cb,NOW(),NOW())",
                    new { n = Name, e = Email, pw = Password,
                          ph = Phone ?? "", ad = Address ?? "", cb = adminId });

                var newId = (int)db.QueryFirst<long>("SELECT LAST_INSERT_ID()");

                db.Execute(
                    @"INSERT INTO private_cleaning_companies
                        (company_name,license_number,contact_person,phone,email,
                         address,services_offered,status,is_active,rep_user_id,created_at,updated_at)
                      VALUES
                        (@cn,@ln,@cp,@ph,@em,@ca,@so,'Active',1,@rid,NOW(),NOW())",
                    new { cn = CompanyName, ln = LicenseNumber ?? "", cp = Name,
                          ph = Phone ?? "", em = Email,
                          ca = CompanyAddress ?? "", so = ServicesOffered ?? "",
                          rid = newId });

                return Redirect($"/Dashboard/SuperAdmin/Users?ok={Uri.EscapeDataString($"Private rep '{Name}' ({CompanyName}) registered")}");
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Contains("Duplicate") ? $"Email '{Email}' already exists" : ex.Message;
                return Redirect($"/Dashboard/SuperAdmin/Users?err={Uri.EscapeDataString(msg)}");
            }
        }

        // ── POST /admin/users/update ────────────────────────────────────────
        [HttpPost("update")]
        public IActionResult Update(
            [FromForm] int UserId,
            [FromForm] string Name,
            [FromForm] string Email,
            [FromForm] string Role,
            [FromForm] string? Phone,
            [FromForm] string? Address,
            [FromForm] string? Password,
            [FromForm] bool IsActive = true)
        {
            var check = RequireAdmin(); if (check != null) return check;

            var r = NormalizeRole(Role);
            try
            {
                using var db = new MySqlConnection(_cs);
                if (string.IsNullOrWhiteSpace(Password))
                {
                    db.Execute(
                        @"UPDATE users SET name=@n,email=@e,role=@r,phone=@ph,
                                 address=@ad,is_active=@act,updated_at=NOW()
                          WHERE id=@id",
                        new { n = Name, e = Email, r, ph = Phone ?? "",
                              ad = Address ?? "", act = IsActive, id = UserId });
                }
                else
                {
                    db.Execute(
                        @"UPDATE users SET name=@n,email=@e,role=@r,phone=@ph,
                                 address=@ad,is_active=@act,password=@pw,updated_at=NOW()
                          WHERE id=@id",
                        new { n = Name, e = Email, r, ph = Phone ?? "",
                              ad = Address ?? "", act = IsActive, pw = Password, id = UserId });
                }
                return Redirect($"/Dashboard/SuperAdmin/Users?ok={Uri.EscapeDataString($"User updated with role '{r}'")}");
            }
            catch (Exception ex)
            {
                return Redirect($"/Dashboard/SuperAdmin/Users?err={Uri.EscapeDataString(ex.Message)}");
            }
        }

        // ── POST /admin/users/toggle ────────────────────────────────────────
        [HttpPost("toggle")]
        public IActionResult Toggle([FromForm] int UserId)
        {
            var check = RequireAdmin(); if (check != null) return check;
            using var db = new MySqlConnection(_cs);
            db.Execute("UPDATE users SET is_active = 1 - is_active,updated_at=NOW() WHERE id=@id", new { id = UserId });
            return Redirect("/Dashboard/SuperAdmin/Users?ok=Status+updated");
        }

        // ── POST /admin/users/deactivate ────────────────────────────────────
        [HttpPost("deactivate")]
        public IActionResult Deactivate([FromForm] int UserId)
        {
            var check = RequireAdmin(); if (check != null) return check;
            using var db = new MySqlConnection(_cs);
            db.Execute("UPDATE users SET is_active=0,updated_at=NOW() WHERE id=@id", new { id = UserId });
            return Redirect("/Dashboard/SuperAdmin/Users?ok=User+deactivated");
        }

        // ── POST /admin/users/reset-password ───────────────────────────────
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromForm] int UserId, [FromForm] string Password)
        {
            var check = RequireAdmin(); if (check != null) return check;
            if (string.IsNullOrWhiteSpace(Password))
                return Redirect("/Dashboard/SuperAdmin/Users?err=Password+cannot+be+empty");
            using var db = new MySqlConnection(_cs);
            db.Execute("UPDATE users SET password=@pw,updated_at=NOW() WHERE id=@id",
                new { pw = Password, id = UserId });
            return Redirect("/Dashboard/SuperAdmin/Users?ok=Password+reset");
        }

        // ── GET /admin/users/search-employees?q=... ────────────────────────
        // Returns employees matching name or phone for the Add User modal live search
        [HttpGet("search-employees")]
        public IActionResult SearchEmployees([FromQuery] string? q)
        {
            var check = RequireAdmin(); if (check != null) return check;
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new List<object>());

            try
            {
                using var db = new MySqlConnection(_cs);
                var rows = db.Query(
                    @"SELECT e.id,
                             e.first_name  AS firstName,
                             e.last_name   AS lastName,
                             CONCAT(e.first_name,' ',e.last_name) AS fullName,
                             COALESCE(e.department,'')     AS department,
                             COALESCE(e.phone_number,'')   AS phone,
                             COALESCE(e.email_address,'')  AS email,
                             e.user_id                     AS userId,
                             u.name                        AS existingUserName,
                             u.email                       AS existingUserEmail
                      FROM employees e
                      LEFT JOIN users u ON u.id = e.user_id
                      WHERE e.employment_status = 'Active'
                        AND (CONCAT(e.first_name,' ',e.last_name) LIKE @s
                             OR e.phone_number LIKE @s)
                      ORDER BY e.first_name
                      LIMIT 15",
                    new { s = $"%{q}%" });

                // Project into a clean object with a bool hasAccount flag
                var results = rows.Select(r => new {
                    id                = (int)r.id,
                    firstName         = (string?)r.firstName ?? "",
                    lastName          = (string?)r.lastName  ?? "",
                    fullName          = (string?)r.fullName  ?? "",
                    department        = (string?)r.department ?? "",
                    phone             = (string?)r.phone     ?? "",
                    email             = (string?)r.email     ?? "",
                    userId            = (int?)r.userId,
                    hasAccount        = r.userId != null && (int?)r.userId > 0,
                    existingUserName  = (string?)r.existingUserName  ?? "",
                    existingUserEmail = (string?)r.existingUserEmail ?? ""
                });

                return Json(results);
            }
            catch
            {
                return Json(new List<object>());
            }
        }

        // ── Role normalizer (delegates to shared RoleHelper) ───────────────
        public static string NormalizeRole(string raw) => RoleHelper.NormalizeRole(raw);
    }
}
