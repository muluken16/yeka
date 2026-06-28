using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.WeredaMahberat
{
    public class TransportRequestsModel : PageModel
    {
        private readonly string _cs;

        public TransportRequestsModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // Stats
        public int TotalRequests   { get; set; }
        public int PendingRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int CompletedRequests  { get; set; }
        public int RejectedRequests   { get; set; }

        // Request list
        public List<dynamic> Requests { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public IActionResult OnGet()
        {
            var userId   = HttpContext.Session.GetInt32("UserId") ?? 0;
            var userName = HttpContext.Session.GetString("UserName");

            if (userId <= 0 || string.IsNullOrEmpty(userName))
                return RedirectToPage("/Login");

            using var db = new MySqlConnection(_cs);

            // Stats for this mahberat user
            TotalRequests = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM transport_requests WHERE mahberat_user_id=@uid", new { uid = userId });
            PendingRequests = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM transport_requests WHERE mahberat_user_id=@uid AND status='PendingDispatcher'", new { uid = userId });
            InProgressRequests = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM transport_requests WHERE mahberat_user_id=@uid AND status NOT IN ('PendingDispatcher','Paid','Completed','DispatcherRejected','StaffRejected')", new { uid = userId });
            CompletedRequests = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM transport_requests WHERE mahberat_user_id=@uid AND status IN ('Paid','Completed')", new { uid = userId });
            RejectedRequests = db.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM transport_requests WHERE mahberat_user_id=@uid AND status IN ('DispatcherRejected','StaffRejected')", new { uid = userId });

            // Build query with optional filters
            var where = "WHERE mahberat_user_id=@uid";
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
                where += " AND status=@status";
            if (!string.IsNullOrWhiteSpace(Search))
                where += " AND (request_number LIKE @search OR pickup_location LIKE @search OR destination LIKE @search OR driver_name LIKE @search)";

            Requests = db.Query<dynamic>($@"
                SELECT request_number, pickup_location, destination,
                       driver_name, vehicle_plate,
                       requested_date, requested_time,
                       status, transport_cost,
                       created_at, updated_at
                FROM transport_requests
                {where}
                ORDER BY created_at DESC
                LIMIT 200",
                new
                {
                    uid    = userId,
                    status = StatusFilter ?? "",
                    search = "%" + (Search ?? "") + "%"
                }).ToList();

            return Page();
        }
    }
}
