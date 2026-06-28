using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.DispatchOfficer
{
    public class VehicleDriverReportModel : PageModel
    {
        private readonly string _cs;

        public VehicleDriverReportModel(IConfiguration cfg)
        {
            _cs = cfg.GetConnectionString("DefaultConnection") ?? "";
        }

        public string UserName { get; set; } = "Dispatch Officer";

        public int TotalVehicles { get; set; }
        public int AvailableVehicles { get; set; }
        public int AssignedVehicles { get; set; }
        public int MaintenanceVehicles { get; set; }

        public int TotalDrivers { get; set; }
        public int ActiveDrivers { get; set; }

        public List<dynamic> VehicleList { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole") ?? "";
            UserName = HttpContext.Session.GetString("UserName") ?? "Dispatch Officer";

            if (HttpContext.Session.GetInt32("UserId") == null) return RedirectToPage("/Login");
            if (role.ToLower() is not "dispatchofficer" and not "dispatch_officer" and not "manager" and not "superadmin")
                return RedirectToPage("/Login");

            try
            {
                using var db = new MySqlConnection(_cs);

                TotalVehicles = db.ExecuteScalar<int>("SELECT COUNT(*) FROM vehicles");
                AvailableVehicles = db.ExecuteScalar<int>("SELECT COUNT(*) FROM vehicles WHERE status='Available'");
                AssignedVehicles = db.ExecuteScalar<int>("SELECT COUNT(*) FROM vehicles WHERE status='Assigned'");
                MaintenanceVehicles = db.ExecuteScalar<int>("SELECT COUNT(*) FROM vehicles WHERE status='Maintenance'");

                TotalDrivers = db.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE role='driver'");
                ActiveDrivers = db.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE role='driver' AND is_active=TRUE");

                VehicleList = db.Query<dynamic>(@"
                    SELECT v.plate_number, v.vehicle_type, v.model, v.color, v.status, 
                           COALESCE(u.name, 'No Driver') as driver_name,
                           COALESCE(u.phone, '—') as driver_phone
                    FROM vehicles v
                    LEFT JOIN users u ON v.driver_id = u.id AND u.role='driver'
                    ORDER BY v.status ASC, v.plate_number ASC").ToList();
            }
            catch { }

            return Page();
        }
    }
}
