using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Controllers;

namespace CleaningManagmentSystem.Pages.Dashboard.Manager
{
    public class StaffModel : PageModel
    {
        private readonly string _cs;

        public StaffModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── Data ──────────────────────────────────────────────────────────────
        public List<StaffRow> StaffList   { get; set; } = new();
        public List<string>   Roles       { get; set; } = new();
        public string SearchQuery  { get; set; } = "";
        public string FilterRole   { get; set; } = "";
        public string FilterStatus { get; set; } = "";

        // ── Summary Stats ─────────────────────────────────────────────────────
        public int     TotalStaff      { get; set; }
        public int     ActiveStaff     { get; set; }
        public int     InactiveStaff   { get; set; }
        public int     DriverCount     { get; set; }
        public int     TotalReceipts   { get; set; }
        public decimal TotalKgAll      { get; set; }
        public decimal TotalAmountAll  { get; set; }

        // ── Chart: staff per role ─────────────────────────────────────────────
        public List<string>  ChartRoleLabels { get; set; } = new();
        public List<int>     ChartRoleValues { get; set; } = new();

        // ── Chart: receipts per staff (top 8) ────────────────────────────────
        public List<string>  ChartTopNames  { get; set; } = new();
        public List<int>     ChartTopCounts { get; set; } = new();

        // ── Messages ──────────────────────────────────────────────────────────
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet(
            [FromQuery] string? search = null,
            [FromQuery] string? role   = null,
            [FromQuery] string? status = null,
            [FromQuery] string? ok     = null,
            [FromQuery] string? err    = null)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            SearchQuery    = search ?? "";
            FilterRole     = role   ?? "";
            FilterStatus   = status ?? "";
            SuccessMessage = ok  ?? "";
            ErrorMessage   = err ?? "";

            LoadData();
            return Page();
        }

        // ── POST: Add staff (no duplicate email) ──────────────────────────────
        public IActionResult OnPostAddStaff()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            string F(string k) => Request.Form[k].ToString().Trim();

            try
            {
                var name  = F("name");
                var email = F("email");
                var pw    = F("password");
                var role  = F("role");
                var phone = F("phone");
                var addr  = F("address");

                if (string.IsNullOrWhiteSpace(name))  throw new Exception("Full name is required.");
                if (string.IsNullOrWhiteSpace(email)) throw new Exception("Email is required.");
                if (string.IsNullOrWhiteSpace(pw))    throw new Exception("Password is required.");
                if (string.IsNullOrWhiteSpace(role))  throw new Exception("Role is required.");
                if (pw.Length < 6)                    throw new Exception("Password must be at least 6 characters.");

                using var db = new MySqlConnection(_cs);

                // Strict duplicate check
                var dup = db.QueryFirstOrDefault<int>(
                    "SELECT COUNT(*) FROM users WHERE LOWER(email) = LOWER(@e)", new { e = email });
                if (dup > 0) throw new Exception($"A user with email '{email}' already exists.");

                var normalizedRole = AdminUsersController.NormalizeRole(role);
                var createdBy = HttpContext.Session.GetInt32("UserId") ?? 0;

                db.Execute(
                    @"INSERT INTO users (name, email, password, role, phone, address, is_active, created_by, created_at, updated_at)
                      VALUES (@n, @e, @pw, @r, @ph, @ad, 1, @cb, NOW(), NOW())",
                    new { n = name, e = email, pw, r = normalizedRole,
                          ph = phone, ad = addr, cb = createdBy });

                SuccessMessage = $"Staff '{name}' ({normalizedRole}) created successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message.Contains("Duplicate entry") || ex.Message.Contains("already exists")
                    ? $"That email is already registered. {ex.Message}"
                    : ex.Message;
            }

            LoadData();
            return Page();
        }

        // ── POST: Update staff ────────────────────────────────────────────────
        public IActionResult OnPostUpdateStaff()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            string F(string k) => Request.Form[k].ToString().Trim();
            int.TryParse(F("id"), out int id);

            try
            {
                var name  = F("name");
                var phone = F("phone");
                var addr  = F("address");
                var role  = F("role");
                var newPw = F("newPassword");

                if (string.IsNullOrWhiteSpace(name)) throw new Exception("Name is required.");

                var normalizedRole = AdminUsersController.NormalizeRole(role);
                using var db = new MySqlConnection(_cs);

                if (!string.IsNullOrEmpty(newPw))
                {
                    if (newPw.Length < 6) throw new Exception("Password must be at least 6 characters.");
                    db.Execute(
                        @"UPDATE users SET name=@n, phone=@ph, address=@ad,
                                          role=@r, password=@pw, updated_at=NOW()
                          WHERE id=@id",
                        new { n = name, ph = phone, ad = addr, r = normalizedRole, pw = newPw, id });
                }
                else
                {
                    db.Execute(
                        @"UPDATE users SET name=@n, phone=@ph, address=@ad,
                                          role=@r, updated_at=NOW()
                          WHERE id=@id",
                        new { n = name, ph = phone, ad = addr, r = normalizedRole, id });
                }

                SuccessMessage = $"Staff updated successfully.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }

            LoadData();
            return Page();
        }

        // ── POST: Toggle active status ────────────────────────────────────────
        public IActionResult OnPostToggleStatus(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            try
            {
                using var db = new MySqlConnection(_cs);
                var current = db.QueryFirstOrDefault<bool>(
                    "SELECT is_active FROM users WHERE id=@id", new { id });
                db.Execute(
                    "UPDATE users SET is_active=@v, updated_at=NOW() WHERE id=@id",
                    new { v = !current, id });
                SuccessMessage = $"Staff {(!current ? "activated" : "deactivated")}.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }

            LoadData();
            return Page();
        }

        // ── POST: Reset password ──────────────────────────────────────────────
        public IActionResult OnPostResetPassword()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            string F(string k) => Request.Form[k].ToString().Trim();
            int.TryParse(F("resetId"), out int id);
            var newPw = F("resetPassword");

            try
            {
                if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 6)
                    throw new Exception("Password must be at least 6 characters.");

                using var db = new MySqlConnection(_cs);
                db.Execute("UPDATE users SET password=@pw, updated_at=NOW() WHERE id=@id",
                    new { pw = newPw, id });
                SuccessMessage = "Password reset successfully.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }

            LoadData();
            return Page();
        }

        // ── Load all data ─────────────────────────────────────────────────────
        private void LoadData()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                // Distinct roles for filter dropdown
                Roles = db.Query<string>(
                    "SELECT DISTINCT COALESCE(role,'') FROM users WHERE role != 'superadmin' AND role IS NOT NULL ORDER BY role")
                    .ToList();

                // Build WHERE clause
                var where = new List<string> { "u.role != 'superadmin'" };
                var p = new DynamicParameters();

                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    where.Add("(u.name LIKE @S OR u.email LIKE @S OR u.phone LIKE @S OR u.role LIKE @S)");
                    p.Add("S", $"%{SearchQuery}%");
                }
                if (!string.IsNullOrEmpty(FilterRole))
                {
                    where.Add("u.role = @R");
                    p.Add("R", FilterRole);
                }
                if (FilterStatus == "active")   where.Add("u.is_active = 1");
                if (FilterStatus == "inactive") where.Add("u.is_active = 0");

                var whereClause = string.Join(" AND ", where);

                // Main query — real columns only, no 'notes'
                var sql = $@"
                    SELECT
                        u.id,
                        COALESCE(u.name,  '')   AS name,
                        COALESCE(u.email, '')   AS email,
                        COALESCE(u.role,  '')   AS role,
                        COALESCE(u.phone, '')   AS phone,
                        COALESCE(u.address,'')  AS address,
                        u.is_active,
                        u.created_at,
                        u.updated_at,
                        /* Receipt counts */
                        (SELECT COUNT(*)
                            FROM staff_receipts sr
                            WHERE sr.driver_id = u.id)                          AS mahberatCount,
                        (SELECT COUNT(*)
                            FROM outsource_receipts orec
                            WHERE orec.driver_id = u.id)                        AS outsourceCount,
                        (SELECT COUNT(*)
                            FROM private_company_receipts pcr
                            WHERE pcr.driver_id = u.id)                         AS privateCount,
                        /* KG totals */
                        COALESCE((SELECT SUM(sr2.kilogram)
                            FROM staff_receipts sr2
                            WHERE sr2.driver_id = u.id), 0)                     AS totalKg,
                        /* Amount totals */
                        COALESCE((SELECT SUM(sr3.kilogram * sr3.price)
                            FROM staff_receipts sr3
                            WHERE sr3.driver_id = u.id), 0)                     AS totalAmount,
                        /* Last receipt date */
                        (SELECT MAX(sr4.receipt_date)
                            FROM staff_receipts sr4
                            WHERE sr4.driver_id = u.id)                         AS lastReceiptDate,
                        /* Assigned vehicle */
                        COALESCE((SELECT v.plate_number
                            FROM vehicles v
                            WHERE v.driver_id = u.id), '')                                        AS vehiclePlate
                    FROM users u
                    WHERE {whereClause}
                    ORDER BY u.is_active DESC, u.name ASC";

                StaffList = db.Query<StaffRow>(sql, p).ToList();

                // Aggregate stats
                TotalStaff     = StaffList.Count;
                ActiveStaff    = StaffList.Count(s => s.is_active);
                InactiveStaff  = StaffList.Count(s => !s.is_active);
                DriverCount    = StaffList.Count(s =>
                    s.role.Equals("driver", StringComparison.OrdinalIgnoreCase));
                TotalReceipts  = StaffList.Sum(s => s.TotalReceipts);
                TotalKgAll     = StaffList.Sum(s => s.totalKg);
                TotalAmountAll = StaffList.Sum(s => s.totalAmount);

                // Chart: staff per role
                var byRole = StaffList
                    .GroupBy(s => string.IsNullOrEmpty(s.role) ? "No Role" : s.role)
                    .OrderByDescending(g => g.Count())
                    .ToList();
                ChartRoleLabels = byRole.Select(g => g.Key).ToList();
                ChartRoleValues = byRole.Select(g => g.Count()).ToList();

                // Chart: top 8 staff by receipt count
                var top8 = StaffList
                    .Where(s => s.TotalReceipts > 0)
                    .OrderByDescending(s => s.TotalReceipts)
                    .Take(8)
                    .ToList();
                ChartTopNames  = top8.Select(s => s.name.Length > 12 ? s.name[..12] + "…" : s.name).ToList();
                ChartTopCounts = top8.Select(s => s.TotalReceipts).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manager.Staff] {ex.Message}");
                ErrorMessage = $"Database error: {ex.Message}";
            }
        }
    }

    // ── View model ────────────────────────────────────────────────────────────
    public class StaffRow
    {
        public int      id              { get; set; }
        public string   name            { get; set; } = "";
        public string   email           { get; set; } = "";
        public string   role            { get; set; } = "";
        public string   phone           { get; set; } = "";
        public string   address         { get; set; } = "";
        public bool     is_active       { get; set; }
        public DateTime created_at      { get; set; }
        public DateTime updated_at      { get; set; }
        public int      mahberatCount   { get; set; }
        public int      outsourceCount  { get; set; }
        public int      privateCount    { get; set; }
        public decimal  totalKg         { get; set; }
        public decimal  totalAmount     { get; set; }
        public DateTime? lastReceiptDate{ get; set; }
        public string   vehiclePlate    { get; set; } = "";

        public int TotalReceipts => mahberatCount + outsourceCount + privateCount;
    }
}
