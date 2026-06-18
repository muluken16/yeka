using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.DispatchOfficer
{
    public class IndexModel : PageModel
    {
        private readonly string _cs;

        public IndexModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // Stats
        public int PendingTransport   { get; set; }
        public int ActiveDispatches   { get; set; }
        public int CompletedToday     { get; set; }
        public int TotalDrivers       { get; set; }
        public int TotalTransport     { get; set; }
        public int PendingDispatches  { get; set; }
        public int MeetingRoomsToday  { get; set; }

        // Recent data
        public List<dynamic> RecentTransport  { get; set; } = new();
        public List<dynamic> RecentDispatches { get; set; } = new();

        public string UserName { get; set; } = "";

        public IActionResult OnGet()
        {
            var uid  = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole") ?? "";
            UserName = HttpContext.Session.GetString("UserName") ?? "Dispatch Officer";

            if (uid == null) return RedirectToPage("/Login");

            try
            {
                using var db = new MySqlConnection(_cs);

                // Transport request stats
                PendingTransport = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM transport_requests WHERE status='PendingDispatcher'");
                TotalTransport = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM transport_requests");

                // Dispatch stats
                PendingDispatches = db.ExecuteScalar<int>(
                    "SELECT COALESCE(COUNT(*),0) FROM dispatches WHERE status='Pending'");
                ActiveDispatches = db.ExecuteScalar<int>(
                    "SELECT COALESCE(COUNT(*),0) FROM dispatches WHERE status IN ('Pending','In Progress','Assigned')");
                CompletedToday = db.ExecuteScalar<int>(
                    "SELECT COALESCE(COUNT(*),0) FROM dispatches WHERE status='Completed' AND DATE(created_at)=CURDATE()");

                // Drivers
                TotalDrivers = db.ExecuteScalar<int>(
                    "SELECT COALESCE(COUNT(*),0) FROM drivers WHERE is_active=1");

                // Meeting rooms booked today
                MeetingRoomsToday = db.ExecuteScalar<int>(
                    "SELECT COALESCE(COUNT(*),0) FROM meeting_room_bookings WHERE DATE(booking_date)=CURDATE()");

                // Recent transport requests (last 5)
                RecentTransport = db.Query<dynamic>(@"
                    SELECT request_number, mahberat_user_name, pickup_location,
                           destination, status, created_at
                    FROM transport_requests
                    ORDER BY created_at DESC LIMIT 5").ToList();

                // Recent dispatches (last 5)
                RecentDispatches = db.Query<dynamic>(@"
                    SELECT dispatch_number, origin, destination,
                           driver_name, status, created_at
                    FROM dispatches
                    ORDER BY created_at DESC LIMIT 5").ToList();
            }
            catch { /* tables may not exist yet — show zeros */ }

            return Page();
        }
    }
}
