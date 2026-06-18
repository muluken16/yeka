using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Models;
using DriverModel = CleaningManagmentSystem.Models.Driver;

namespace CleaningManagmentSystem.Pages.Dashboard.DispatchOfficer
{
    [IgnoreAntiforgeryToken]
    public class VehiclesModel : PageModel
    {
        private readonly string _cs;

        public VehiclesModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        [BindProperty] public int    EditId       { get; set; }
        [BindProperty] public string PlateNumber  { get; set; } = "";
        [BindProperty] public string VehicleType  { get; set; } = "";
        [BindProperty] public string VehicleModel { get; set; } = "";
        [BindProperty] public string Color        { get; set; } = "";
        [BindProperty] public int?   DriverId     { get; set; }
        [BindProperty] public string Status       { get; set; } = "Available";

        public List<Vehicle>     Vehicles { get; set; } = new();
        public List<DriverModel> Drivers  { get; set; } = new();

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        private bool IsAuthorized() =>
            (HttpContext.Session.GetString("UserRole") ?? "").ToLower() is "dispatchofficer" or "dispatch_officer" or "manager" or "superadmin";

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserId") == null) return RedirectToPage("/Login");
            if (!IsAuthorized()) return RedirectToPage("/Login");
            Load();
            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage   = TempData["Error"]?.ToString()   ?? "";
            return Page();
        }

        // ── ADD ───────────────────────────────────────────────────────────────
        public IActionResult OnPostAdd()
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                // Driver is a users.id — fetch name from users table
                var driverName = DriverId > 0
                    ? db.ExecuteScalar<string>("SELECT name FROM users WHERE id=@Id AND role='driver'", new { Id = DriverId }) ?? ""
                    : "";

                db.Execute(@"
                    INSERT INTO vehicles (plate_number, vehicle_type, model, color, driver_id, driver_name, status, created_at, updated_at)
                    VALUES (@Plate, @Type, @Model, @Color, @DrvId, @DrvName, @Status, NOW(), NOW())",
                    new { Plate = PlateNumber.Trim(), Type = VehicleType, Model = VehicleModel.Trim(),
                          Color, DrvId = DriverId, DrvName = driverName, Status });

                TempData["Success"] = $"Vehicle {PlateNumber} registered successfully.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToPage();
        }

        // ── UPDATE ────────────────────────────────────────────────────────────
        public IActionResult OnPostUpdate()
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                var driverName = DriverId > 0
                    ? db.ExecuteScalar<string>("SELECT name FROM users WHERE id=@Id AND role='driver'", new { Id = DriverId }) ?? ""
                    : "";

                db.Execute(@"
                    UPDATE vehicles
                    SET plate_number=@Plate, vehicle_type=@Type, model=@Model, color=@Color,
                        driver_id=@DrvId, driver_name=@DrvName, status=@Status, updated_at=NOW()
                    WHERE id=@Id",
                    new { Plate = PlateNumber.Trim(), Type = VehicleType, Model = VehicleModel.Trim(),
                          Color, DrvId = DriverId, DrvName = driverName, Status, Id = EditId });

                TempData["Success"] = "Vehicle updated.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToPage();
        }

        // ── DELETE ────────────────────────────────────────────────────────────
        public IActionResult OnPostDelete(int id)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute("DELETE FROM vehicles WHERE id=@Id", new { Id = id });
                TempData["Success"] = "Vehicle deleted.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToPage();
        }

        // ── STATUS TOGGLE ─────────────────────────────────────────────────────
        public IActionResult OnPostSetStatus(int id, string status)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute("UPDATE vehicles SET status=@S, updated_at=NOW() WHERE id=@Id",
                    new { S = status, Id = id });
                TempData["Success"] = $"Status set to {status}.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToPage();
        }

        private void Load()
        {
            using var db = new MySqlConnection(_cs);
            try
            {
                Vehicles = db.Query<Vehicle>(@"
                    SELECT v.id AS Id, v.plate_number AS PlateNumber, v.vehicle_type AS VehicleType,
                           v.model AS Model, v.color AS Color, v.driver_id AS DriverId,
                           v.driver_name AS DriverName, v.status AS Status,
                           v.created_at AS CreatedAt, v.updated_at AS UpdatedAt
                    FROM vehicles v
                    ORDER BY v.plate_number ASC").ToList();
            }
            catch { }

            try
            {
                // Load drivers from app users table (role='driver') — same source
                // as the transport assignment so vehicle assignment is consistent.
                Drivers = db.Query<DriverModel>(@"
                    SELECT id AS Id, name AS FullName, phone AS Phone,
                           '' AS Email, '' AS LicenseNumber, '' AS LicenseType,
                           CURDATE() AS LicenseExpiry, '' AS Address,
                           is_active AS IsActive, created_at AS CreatedAt
                    FROM users
                    WHERE role = 'driver' AND is_active = TRUE
                    ORDER BY name ASC").ToList();
            }
            catch { }
        }
    }
}
