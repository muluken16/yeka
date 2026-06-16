using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Manager
{
    public class PrivateCleaningCompanyModel : PageModel
    {
        private readonly string _cs;

        public List<ManagerPrivateCompanyRow> Companies     { get; set; } = new();
        public List<ManagerAvailableUser>     AvailableUsers { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        public PrivateCleaningCompanyModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── GET ──────────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            LoadData();
            return Page();
        }

        // ── POST ─────────────────────────────────────────────────────────────
        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            string F(string k) => Request.Form[k].ToString().Trim();

            var action = F("action");
            int.TryParse(F("Id"), out int id);

            try
            {
                using var db = new MySqlConnection(_cs);

                switch (action)
                {
                    // ── Create company ────────────────────────────────────
                    case "create":
                        var cn = F("CompanyName");
                        if (string.IsNullOrWhiteSpace(cn))
                            throw new Exception("Company Name is required.");

                        int? repUserId = null;
                        if (int.TryParse(F("RepUserId"), out int uidVal) && uidVal > 0)
                            repUserId = uidVal;

                        db.Execute(
                            @"INSERT INTO private_cleaning_companies
                                (company_name, contact_person, phone, email,
                                 license_number, status, services_offered, address,
                                 is_active, rep_user_id, created_at, updated_at)
                              VALUES
                                (@cn, @cp, @ph, @em, @ln, @st, @sv, @ad, 1, @repUserId, NOW(), NOW())",
                            new {
                                cn, cp = F("ContactPerson"), ph = F("Phone"),
                                em = F("Email"), ln = F("LicenseNumber"),
                                st = string.IsNullOrEmpty(F("Status")) ? "Active" : F("Status"),
                                sv = F("ServicesOffered"), ad = F("Address"),
                                repUserId
                            });

                        if (repUserId.HasValue)
                            db.Execute(
                                "UPDATE users SET role='PrivateCompanyRep', updated_at=NOW() WHERE id=@uid",
                                new { uid = repUserId.Value });

                        SuccessMessage = $"Company '{cn}' created successfully!";
                        break;

                    // ── Update company ────────────────────────────────────
                    case "update":
                        int? updateRepUserId = null;
                        if (int.TryParse(F("RepUserId"), out int uidValUpdate) && uidValUpdate > 0)
                            updateRepUserId = uidValUpdate;

                        var existingRepUserId = db.QueryFirstOrDefault<int?>(
                            "SELECT rep_user_id FROM private_cleaning_companies WHERE id=@id",
                            new { id });

                        var finalRepUserId = updateRepUserId ?? existingRepUserId;

                        if (finalRepUserId.HasValue && finalRepUserId != existingRepUserId)
                            db.Execute(
                                "UPDATE users SET role='PrivateCompanyRep', updated_at=NOW() WHERE id=@uid",
                                new { uid = finalRepUserId.Value });

                        db.Execute(
                            @"UPDATE private_cleaning_companies SET
                                company_name=@cn, contact_person=@cp, phone=@ph,
                                email=@em, license_number=@ln, status=@st,
                                services_offered=@sv, address=@ad,
                                rep_user_id=@repUserId, updated_at=NOW()
                              WHERE id=@id",
                            new {
                                cn = F("CompanyName"), cp = F("ContactPerson"),
                                ph = F("Phone"), em = F("Email"),
                                ln = F("LicenseNumber"), st = F("Status"),
                                sv = F("ServicesOffered"), ad = F("Address"),
                                repUserId = finalRepUserId, id
                            });

                        SuccessMessage = "Company updated.";
                        break;

                    // ── Toggle status ─────────────────────────────────────
                    case "toggle":
                        db.Execute(
                            @"UPDATE private_cleaning_companies
                              SET status    = CASE WHEN status='Active' THEN 'Inactive' ELSE 'Active' END,
                                  is_active = CASE WHEN status='Active' THEN 0 ELSE 1 END,
                                  updated_at = NOW()
                              WHERE id=@id", new { id });
                        SuccessMessage = "Status toggled.";
                        break;

                    // ── Link existing user ────────────────────────────────
                    case "link_user":
                        int.TryParse(F("UserId"), out int userId);
                        if (userId == 0) throw new Exception("Please select a user.");

                        var userInfo = db.QueryFirstOrDefault<dynamic>(
                            "SELECT id, name, email FROM users WHERE id=@uid AND is_active=1",
                            new { uid = userId });
                        if (userInfo == null) throw new Exception("User not found or inactive.");

                        // Unlink from previous company if needed
                        var prevCompanyId = db.QueryFirstOrDefault<int?>(
                            "SELECT id FROM private_cleaning_companies WHERE rep_user_id=@uid AND id != @id",
                            new { uid = userId, id });
                        if (prevCompanyId.HasValue)
                            db.Execute(
                                "UPDATE private_cleaning_companies SET rep_user_id=NULL, updated_at=NOW() WHERE id=@old",
                                new { old = prevCompanyId.Value });

                        db.Execute("UPDATE users SET role='PrivateCompanyRep', updated_at=NOW() WHERE id=@uid", new { uid = userId });
                        db.Execute("UPDATE private_cleaning_companies SET rep_user_id=@uid, updated_at=NOW() WHERE id=@id", new { uid = userId, id });

                        SuccessMessage = $"User '{(string)userInfo.name}' linked as PrivateCompanyRep.";
                        break;

                    // ── Unlink user ───────────────────────────────────────
                    case "unlink_user":
                        db.Execute(
                            "UPDATE private_cleaning_companies SET rep_user_id=NULL, updated_at=NOW() WHERE id=@id",
                            new { id });
                        SuccessMessage = "User unlinked from company.";
                        break;

                    // ── Register & link new user ──────────────────────────
                    case "register_and_link_user":
                        var newName  = F("NewUserName");
                        var newEmail = F("NewUserEmail");
                        var newPass  = F("NewUserPassword");

                        if (string.IsNullOrWhiteSpace(newName))  throw new Exception("Representative name is required.");
                        if (string.IsNullOrWhiteSpace(newEmail)) throw new Exception("Representative email is required.");
                        if (string.IsNullOrWhiteSpace(newPass))  throw new Exception("Password is required.");

                        var emailExists = db.QueryFirstOrDefault<int>(
                            "SELECT COUNT(*) FROM users WHERE email=@e", new { e = newEmail });
                        if (emailExists > 0) throw new Exception($"Email '{newEmail}' is already registered.");

                        var adminId = HttpContext.Session.GetInt32("UserId") ?? 0;
                        db.Execute(
                            @"INSERT INTO users (name, email, password, role, phone, address, is_active, created_by, created_at, updated_at)
                              VALUES (@n, @e, @pw, 'PrivateCompanyRep', @ph, @ad, 1, @cb, NOW(), NOW())",
                            new { n = newName, e = newEmail, pw = newPass,
                                  ph = F("NewUserPhone"), ad = F("NewUserAddress"), cb = adminId });

                        var newUserId = db.QueryFirst<int>("SELECT LAST_INSERT_ID()");
                        db.Execute(
                            "UPDATE private_cleaning_companies SET rep_user_id=@uid, updated_at=NOW() WHERE id=@id",
                            new { uid = newUserId, id });

                        SuccessMessage = $"Representative '{newName}' registered and linked successfully.";
                        break;

                    default:
                        ErrorMessage = $"Unknown action: '{action}'";
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manager.PrivateCompany] ERROR: {ex.Message}");
                ErrorMessage = ex.Message.Contains("Duplicate entry")
                    ? "That email is already registered."
                    : ex.Message;
            }

            LoadData();
            return Page();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void LoadData()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                Companies = db.Query<ManagerPrivateCompanyRow>(
                    @"SELECT p.id,
                             p.company_name  AS CompanyName,
                             p.license_number AS LicenseNumber,
                             p.contact_person AS ContactPerson,
                             p.phone, p.email, p.address,
                             p.services_offered AS ServicesOffered,
                             p.status,
                             u.id     AS RepUserId,
                             u.name   AS RepUserName,
                             u.email  AS RepUserEmail,
                             u.phone  AS RepUserPhone,
                             u.is_active AS RepUserActive
                      FROM private_cleaning_companies p
                      LEFT JOIN users u ON u.id = p.rep_user_id
                      ORDER BY p.id DESC").ToList();

                AvailableUsers = db.Query<ManagerAvailableUser>(
                    @"SELECT u.id, u.name, u.email, u.role, u.phone,
                             COALESCE(p.company_name,'') AS LinkedCompany
                      FROM users u
                      LEFT JOIN private_cleaning_companies p ON p.rep_user_id = u.id
                      WHERE u.is_active = 1
                      ORDER BY u.name ASC").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manager.PrivateCompany.LoadData] {ex.Message}");
                ErrorMessage = "Error loading data.";
            }
        }
    }

    // ── View Models ────────────────────────────────────────────────────────────
    public class ManagerPrivateCompanyRow
    {
        public int     Id              { get; set; }
        public string  CompanyName     { get; set; } = "";
        public string  ContactPerson   { get; set; } = "";
        public string  Phone           { get; set; } = "";
        public string  Email           { get; set; } = "";
        public string  LicenseNumber   { get; set; } = "";
        public string  Status          { get; set; } = "Active";
        public string  ServicesOffered { get; set; } = "";
        public string  Address         { get; set; } = "";
        public int?    RepUserId       { get; set; }
        public string? RepUserName     { get; set; }
        public string? RepUserEmail    { get; set; }
        public string? RepUserPhone    { get; set; }
        public bool?   RepUserActive   { get; set; }
    }

    public class ManagerAvailableUser
    {
        public int    Id            { get; set; }
        public string Name          { get; set; } = "";
        public string Email         { get; set; } = "";
        public string Role          { get; set; } = "";
        public string Phone         { get; set; } = "";
        public string LinkedCompany { get; set; } = "";
    }
}
