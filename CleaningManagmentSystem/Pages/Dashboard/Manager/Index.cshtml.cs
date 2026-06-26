using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Manager
{
    public class IndexModel : PageModel
    {
        private readonly string _cs;

        public IndexModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── Stats ──────────────────────────────────────────────────────────
        public int TotalReceipts     { get; set; }
        public int PendingApprovals  { get; set; }
        public int TotalEmployees    { get; set; }
        public int TotalVehicles     { get; set; }
        public int TotalWeredas      { get; set; }
        public int TotalMahberats    { get; set; }
        public decimal TotalKgToday  { get; set; }
        public decimal TotalKgMonth  { get; set; }

        // ── Chart: last 14 days kg collected ──────────────────────────────
        public List<ChartRow> DailyChart { get; set; } = new();

        // ── Recent pending approvals ───────────────────────────────────────
        public List<dynamic> RecentPending { get; set; } = new();

        // ── Receipt breakdown by type ──────────────────────────────────────
        public int MahberatCount  { get; set; }
        public int OutsourceCount { get; set; }
        public int PrivateCount   { get; set; }

        public string UserName { get; set; } = "";

        private bool IsAuth() =>
            (HttpContext.Session.GetString("UserRole") ?? "").ToLower()
                is "manager" or "superadmin" or "hr";

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserId") == null) return RedirectToPage("/Login");
            if (!IsAuth()) return RedirectToPage("/Login");

            UserName = HttpContext.Session.GetString("UserName") ?? "Manager";

            try
            {
                using var db = new MySqlConnection(_cs);

                // Basic counts
                TotalWeredas   = db.ExecuteScalar<int>("SELECT COUNT(*) FROM weredas   WHERE is_active=1");
                TotalMahberats = db.ExecuteScalar<int>("SELECT COUNT(*) FROM mahberats WHERE is_active=1");
                TotalVehicles  = db.ExecuteScalar<int>("SELECT COUNT(*) FROM vehicles");
                TotalEmployees = db.ExecuteScalar<int>("SELECT COUNT(*) FROM employees WHERE employment_status='Active'");

                // Receipt counts
                MahberatCount  = db.ExecuteScalar<int>("SELECT COUNT(*) FROM staff_receipts");
                OutsourceCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM outsource_receipts");
                try { PrivateCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM private_company_receipts"); } catch { }

                TotalReceipts = MahberatCount + OutsourceCount + PrivateCount;

                // Pending approvals
                PendingApprovals = db.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM (
                        SELECT id FROM staff_receipts    WHERE status='Pending'
                        UNION ALL
                        SELECT id FROM outsource_receipts WHERE status='Pending'
                    ) t");

                // Kg today & this month
                TotalKgToday = db.ExecuteScalar<decimal>(@"
                    SELECT COALESCE(SUM(kilogram),0) FROM (
                        SELECT kilogram FROM staff_receipts    WHERE DATE(receipt_date)=CURDATE()
                        UNION ALL
                        SELECT kilogram FROM outsource_receipts WHERE DATE(receipt_date)=CURDATE()
                    ) t");

                TotalKgMonth = db.ExecuteScalar<decimal>(@"
                    SELECT COALESCE(SUM(kilogram),0) FROM (
                        SELECT kilogram FROM staff_receipts    WHERE YEAR(receipt_date)=YEAR(CURDATE()) AND MONTH(receipt_date)=MONTH(CURDATE())
                        UNION ALL
                        SELECT kilogram FROM outsource_receipts WHERE YEAR(receipt_date)=YEAR(CURDATE()) AND MONTH(receipt_date)=MONTH(CURDATE())
                    ) t");

                // Daily chart — last 14 days
                DailyChart = db.Query<ChartRow>(@"
                    SELECT DATE_FORMAT(d, '%m/%d') AS Label,
                           ROUND(SUM(kg), 1)       AS Value
                    FROM (
                        SELECT receipt_date AS d, COALESCE(kilogram,0) AS kg FROM staff_receipts
                         WHERE receipt_date >= CURDATE() - INTERVAL 13 DAY
                        UNION ALL
                        SELECT receipt_date AS d, COALESCE(kilogram,0) AS kg FROM outsource_receipts
                         WHERE receipt_date >= CURDATE() - INTERVAL 13 DAY
                    ) t
                    GROUP BY d
                    ORDER BY d ASC").ToList();

                // Recent 5 pending
                RecentPending = db.Query<dynamic>(@"
                    SELECT * FROM (
                        SELECT id, 'Mahberat' AS type,
                               wereda_name, mahberat_name AS entity_name,
                               driver_name, kilogram, status, registered_at
                        FROM staff_receipts WHERE status='Pending'
                        UNION ALL
                        SELECT id, 'Outsource' AS type,
                               wereda_name, company_name AS entity_name,
                               driver_name, kilogram, status, registered_at
                        FROM outsource_receipts WHERE status='Pending'
                    ) t
                    ORDER BY registered_at DESC LIMIT 5").ToList();
            }
            catch { /* tables may not exist yet */ }

            return Page();
        }

        public class ChartRow
        {
            public string  Label { get; set; } = "";
            public decimal Value { get; set; }
        }
    }
}
