using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Staff
{
    public class StaffReportModel : PageModel
    {
        private readonly string _cs;

        // ── Filters ──────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string FilterStatus    { get; set; } = "";
        [BindProperty(SupportsGet = true)] public int?   FilterWeredaId  { get; set; }
        [BindProperty(SupportsGet = true)] public int?   FilterDriverId  { get; set; }
        [BindProperty(SupportsGet = true)] public string FilterStartDate { get; set; } = "";
        [BindProperty(SupportsGet = true)] public string FilterEndDate   { get; set; } = "";
        [BindProperty(SupportsGet = true)] public string GroupBy         { get; set; } = "driver";

        // ── Dropdown data ─────────────────────────────────────────────────────
        public List<dynamic> Weredas { get; set; } = new();
        public List<dynamic> Drivers { get; set; } = new();

        // ── Stats ─────────────────────────────────────────────────────────────
        public int     TotalReceipts { get; set; }
        public decimal TotalKg       { get; set; }
        public decimal TotalAmount   { get; set; }
        public int     TotalApproved { get; set; }
        public int     TotalPending  { get; set; }

        // ── Per-staff summary rows ─────────────────────────────────────────────
        public List<StaffSummaryRow> StaffSummary { get; set; } = new();

        // ── Detailed receipt rows ─────────────────────────────────────────────
        public List<StaffReceiptRow> Receipts { get; set; } = new();

        // ── Chart data ────────────────────────────────────────────────────────
        public List<string>  ChartLabels { get; set; } = new();
        public List<decimal> ChartKg     { get; set; } = new();

        // ── Top drivers chart ─────────────────────────────────────────────────
        public List<string>  TopDriverNames  { get; set; } = new();
        public List<decimal> TopDriverKg     { get; set; } = new();

        public string ErrorMessage { get; set; } = "";

        public StaffReportModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            if (string.IsNullOrEmpty(FilterStartDate))
                FilterStartDate = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(FilterEndDate))
                FilterEndDate = DateTime.Today.ToString("yyyy-MM-dd");

            LoadDropdowns();
            LoadData();
            return Page();
        }

        // ── POST: Export CSV ──────────────────────────────────────────────────
        public IActionResult OnPostExportCsv()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            FilterStatus     = Request.Form["FilterStatus"];
            FilterStartDate  = Request.Form["FilterStartDate"];
            FilterEndDate    = Request.Form["FilterEndDate"];
            GroupBy          = Request.Form["GroupBy"];
            int.TryParse(Request.Form["FilterWeredaId"], out int w); FilterWeredaId = w > 0 ? w : null;
            int.TryParse(Request.Form["FilterDriverId"], out int d); FilterDriverId = d > 0 ? d : null;

            LoadData();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Driver,Wereda,Mahberat,Plate,Date,Time,Kilogram,Rate,Total,Status");
            foreach (var r in Receipts)
                sb.AppendLine($"\"{r.DriverName}\",\"{r.WeredaName}\",\"{r.EntityName}\"," +
                              $"\"{r.PlateName}\",\"{r.ReceiptDate}\",\"{r.ReceiptTime}\"," +
                              $"{r.Kilogram},{r.Price},{r.Total},\"{r.Status}\"");

            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                        "text/csv", $"staff_report_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        private void LoadDropdowns()
        {
            try
            {
                using var db = new MySqlConnection(_cs);
                Weredas = db.Query<dynamic>(
                    "SELECT id, name FROM weredas WHERE is_active=1 ORDER BY name").ToList();
                Drivers = db.Query<dynamic>(
                    "SELECT id, name FROM users WHERE role='driver' AND is_active=1 ORDER BY name").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StaffReport.LoadDropdowns] {ex.Message}");
            }
        }

        private void LoadData()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                var start = string.IsNullOrEmpty(FilterStartDate)
                    ? DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd") : FilterStartDate;
                var end = string.IsNullOrEmpty(FilterEndDate)
                    ? DateTime.Today.ToString("yyyy-MM-dd") : FilterEndDate;

                var p = new DynamicParameters();
                p.Add("Start", start);
                p.Add("End",   end);

                // Build optional filters
                string wf = FilterWeredaId.HasValue   ? " AND sr.wereda_id = @WId" : "";
                string df = FilterDriverId.HasValue   ? " AND sr.driver_id = @DId" : "";
                string sf = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status = @St" : "";
                if (FilterWeredaId.HasValue) p.Add("WId", FilterWeredaId.Value);
                if (FilterDriverId.HasValue) p.Add("DId", FilterDriverId.Value);
                if (!string.IsNullOrEmpty(FilterStatus)) p.Add("St", FilterStatus);

                // ── All Mahberat receipts for this staff report ────────────────
                var sql = $@"
                    SELECT
                        COALESCE(sr.driver_name,  'Unknown')  AS DriverName,
                        COALESCE(sr.wereda_name,  '')         AS WeredaName,
                        COALESCE(sr.mahberat_name,'')         AS EntityName,
                        COALESCE(sr.plate_number, '')         AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d')          AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0)               AS Kilogram,
                        COALESCE(sr.price,0)                  AS Price,
                        ROUND(COALESCE(sr.kilogram,0)*COALESCE(sr.price,0),2) AS Total,
                        sr.status                             AS Status,
                        sr.registered_at                      AS RegisteredAt
                    FROM staff_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End
                      {wf}{df}{sf}
                    ORDER BY sr.registered_at DESC";

                Receipts = db.Query<StaffReceiptRow>(sql, p).ToList();

                // ── Stats ──────────────────────────────────────────────────────
                TotalReceipts = Receipts.Count;
                TotalKg       = Receipts.Sum(r => r.Kilogram);
                TotalAmount   = Receipts.Sum(r => r.Total);
                TotalApproved = Receipts.Count(r => r.Status == "Approved" || r.Status == "Billed" || r.Status == "Paid");
                TotalPending  = Receipts.Count(r => r.Status == "Pending"  || r.Status == "Registered");

                // ── Per-staff summary ──────────────────────────────────────────
                StaffSummary = Receipts
                    .GroupBy(r => r.DriverName)
                    .Select(g => new StaffSummaryRow {
                        DriverName    = g.Key,
                        TotalReceipts = g.Count(),
                        TotalKg       = g.Sum(r => r.Kilogram),
                        TotalAmount   = g.Sum(r => r.Total),
                        Approved      = g.Count(r => r.Status == "Approved" || r.Status == "Billed" || r.Status == "Paid"),
                        Pending       = g.Count(r => r.Status == "Pending"  || r.Status == "Registered"),
                        Rejected      = g.Count(r => r.Status == "Rejected"),
                        LastDate      = g.Max(r => r.ReceiptDate),
                        Weredas       = string.Join(", ", g.Select(r => r.WeredaName)
                                            .Where(x => !string.IsNullOrEmpty(x))
                                            .Distinct().Take(3))
                    })
                    .OrderByDescending(g => g.TotalKg)
                    .ToList();

                // ── Daily KG chart ─────────────────────────────────────────────
                var chartSql = @"
                    SELECT FORMAT(receipt_date,'MM-dd') AS lbl,
                           ROUND(SUM(kilogram),2)            AS kg
                    FROM staff_receipts
                    WHERE receipt_date BETWEEN @Start AND @End
                    GROUP BY receipt_date ORDER BY receipt_date ASC";
                var chartRows = db.Query<dynamic>(chartSql, new { Start = start, End = end }).ToList();
                ChartLabels = chartRows.Select(r => (string)(r.lbl ?? "")).ToList();
                ChartKg     = chartRows.Select(r => (decimal)(r.kg  ?? 0m)).ToList();

                // ── Top 8 drivers by KG ────────────────────────────────────────
                var top8 = StaffSummary.Take(8).ToList();
                TopDriverNames = top8.Select(s => s.DriverName.Length > 14 ? s.DriverName[..14] + "…" : s.DriverName).ToList();
                TopDriverKg    = top8.Select(s => s.TotalKg).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StaffReport.LoadData] {ex.Message}");
                ErrorMessage = $"Database error: {ex.Message}";
            }
        }
    }

    // ── View models ────────────────────────────────────────────────────────────
    public class StaffSummaryRow
    {
        public string  DriverName    { get; set; } = "";
        public int     TotalReceipts { get; set; }
        public decimal TotalKg       { get; set; }
        public decimal TotalAmount   { get; set; }
        public int     Approved      { get; set; }
        public int     Pending       { get; set; }
        public int     Rejected      { get; set; }
        public string  LastDate      { get; set; } = "";
        public string  Weredas       { get; set; } = "";
    }

    public class StaffReceiptRow
    {
        public string  DriverName   { get; set; } = "";
        public string  WeredaName   { get; set; } = "";
        public string  EntityName   { get; set; } = "";
        public string  PlateName    { get; set; } = "";
        public string  ReceiptDate  { get; set; } = "";
        public string  ReceiptTime  { get; set; } = "";
        public decimal Kilogram     { get; set; }
        public decimal Price        { get; set; }
        public decimal Total        { get; set; }
        public string  Status       { get; set; } = "";
        public DateTime RegisteredAt { get; set; }
    }
}
