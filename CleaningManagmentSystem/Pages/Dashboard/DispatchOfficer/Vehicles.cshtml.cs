using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;
using CleaningManagmentSystem.Models;

namespace CleaningManagmentSystem.Pages.Dashboard.DispatchOfficer
{
    [IgnoreAntiforgeryToken]
    public class VehiclesModel : PageModel
    {
        private readonly string _cs;

        public VehiclesModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        [BindProperty] public int    EditId      { get; set; }
        [BindProperty] public string PlateNumber { get; set; } = "";
        [BindProperty] public string VehicleType { get; set; } = "";
        [BindProperty] public string Model       { get; set; } = "";
        [BindProperty] public string Color       { get; set; } = "";
        [BindProperty] public string Status      { get; set; } = "Available";

        public List<Vehicle> Vehicles { get; set; } = new();

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        private bool IsAuthorized() =>
            (HttpContext.Session.GetString("UserRole") ?? "").ToLower()
                is "dispatchofficer" or "dispatch_officer" or "manager" or "superadmin";

        // ── GET ───────────────────────────────────────────────────────────────
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
                db.Execute(@"
                    INSERT INTO vehicles (plate_number, vehicle_type, model, color, status, created_at, updated_at)
                    VALUES (@Plate, @Type, @Model, @Color, @Status, NOW(), NOW())",
                    new { Plate = PlateNumber.Trim(), Type = VehicleType, Model = Model?.Trim() ?? "", Color = Color?.Trim() ?? "", Status });

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
                db.Execute(@"
                    UPDATE vehicles
                    SET plate_number=@Plate, vehicle_type=@Type, model=@Model, color=@Color, status=@Status, updated_at=NOW()
                    WHERE id=@Id",
                    new { Plate = PlateNumber.Trim(), Type = VehicleType, Model = Model?.Trim() ?? "", Color = Color?.Trim() ?? "", Status, Id = EditId });

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
                    SELECT id AS Id, plate_number AS PlateNumber,
                           vehicle_type AS VehicleType, model AS Model, color AS Color, status AS Status,
                           created_at AS CreatedAt, updated_at AS UpdatedAt
                    FROM vehicles
                    ORDER BY plate_number ASC").ToList();
            }
            catch { }
        }
    }
}
