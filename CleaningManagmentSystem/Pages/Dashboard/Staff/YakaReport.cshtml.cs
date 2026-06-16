using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Staff
{
    public class YakaReportModel : PageModel
    {
        private readonly string _cs;

        // ── Filters (all come from dropdowns) ────────────────────────────────
        [BindProperty(SupportsGet = true)] public string FilterType      { get; set; } = "All";
        [BindProperty(SupportsGet = true)] public string FilterStatus    { get; set; } = "";
        [BindProperty(SupportsGet = true)] public int?   FilterWeredaId  { get; set; }
        [BindProperty(SupportsGet = true)] public int?   FilterMahberatId{ get; set; }
        [BindProperty(SupportsGet = true)] public int?   FilterDriverId  { get; set; }
        [BindProperty(SupportsGet = true)] public string FilterStartDate { get; set; } = "";
        [BindProperty(SupportsGet = true)] public string FilterEndDate   { get; set; } = "";
        [BindProperty(SupportsGet = true)] public string GroupBy         { get; set; } = "wereda";
        [BindProperty(SupportsGet = true)] public string ReportView      { get; set; } = "summary"; // summary | detailed

        // ── Dropdown data ─────────────────────────────────────────────────────
        public List<dynamic> Weredas   { get; set; } = new();
        public List<dynamic> Mahberats { get; set; } = new();
        public List<dynamic> Drivers   { get; set; } = new();

        // ── Real receipt rows ─────────────────────────────────────────────────
        public List<YakaReceiptRow> Receipts { get; set; } = new();

        // ── Summary stats ─────────────────────────────────────────────────────
        public int     TotalCount    { get; set; }
        public decimal TotalKg       { get; set; }
        public decimal TotalAmount   { get; set; }
        public int     TotalApproved { get; set; }
        public int     TotalPending  { get; set; }
        public int     TotalRejected { get; set; }
        public int     TotalPaid     { get; set; }

        // ── Chart data (daily) ────────────────────────────────────────────────
        public List<string>  ChartLabels { get; set; } = new();
        public List<decimal> ChartKg     { get; set; } = new();
        public List<decimal> ChartAmount { get; set; } = new();

        // ── Group breakdown for report view ───────────────────────────────────
        public List<YakaGroupRow> GroupedData { get; set; } = new();

        public string ErrorMessage { get; set; } = "";

        public YakaReportModel(IConfiguration cfg)
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

            FilterType       = Request.Form["FilterType"];
            FilterStatus     = Request.Form["FilterStatus"];
            FilterStartDate  = Request.Form["FilterStartDate"];
            FilterEndDate    = Request.Form["FilterEndDate"];
            GroupBy          = Request.Form["GroupBy"];
            ReportView       = Request.Form["ReportView"];
            int.TryParse(Request.Form["FilterWeredaId"],   out int w); FilterWeredaId   = w > 0 ? w : null;
            int.TryParse(Request.Form["FilterMahberatId"], out int m); FilterMahberatId = m > 0 ? m : null;
            int.TryParse(Request.Form["FilterDriverId"],   out int d); FilterDriverId   = d > 0 ? d : null;

            LoadData();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Type,Wereda,Mahberat/Company,Driver,Plate,Date,Time,Kilogram,Rate,Total,Status");
            foreach (var r in Receipts)
                sb.AppendLine($"\"{r.ReceiptType}\",\"{r.WeredaName}\",\"{r.EntityName}\"," +
                              $"\"{r.DriverName}\",\"{r.PlateName}\",\"{r.ReceiptDate}\"," +
                              $"\"{r.ReceiptTime}\",{r.Kilogram},{r.Price},{r.Total},\"{r.Status}\"");

            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                        "text/csv", $"yaka_report_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void LoadDropdowns()
        {
            try
            {
                using var db = new MySqlConnection(_cs);
                Weredas   = db.Query<dynamic>("SELECT id, name FROM weredas   WHERE is_active=1 ORDER BY name").ToList();
                Mahberats = db.Query<dynamic>("SELECT id, name FROM mahberats WHERE is_active=1 ORDER BY name").ToList();
                Drivers   = db.Query<dynamic>("SELECT id, name FROM users WHERE role='driver' AND is_active=1 ORDER BY name").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YakaReport.LoadDropdowns] {ex.Message}");
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

                bool doMahberat  = FilterType == "All" || FilterType == "Mahberat";
                bool doOutsource = FilterType == "All" || FilterType == "Outsource";
                bool doPrivate   = FilterType == "All" || FilterType == "Private";

                var parts = new List<string>();

                // ── staff_receipts ─────────────────────────────────────────────
                if (doMahberat)
                {
                    var wf = FilterWeredaId.HasValue   ? " AND sr.wereda_id=@WId"   : "";
                    var mf = FilterMahberatId.HasValue ? " AND sr.mahberat_id=@MId" : "";
                    var df = FilterDriverId.HasValue   ? " AND sr.driver_id=@DId"   : "";
                    var sf = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status=@St" : "";
                    if (FilterWeredaId.HasValue)   p.Add("WId", FilterWeredaId.Value);
                    if (FilterMahberatId.HasValue) p.Add("MId", FilterMahberatId.Value);
                    if (FilterDriverId.HasValue)   p.Add("DId", FilterDriverId.Value);
                    if (!string.IsNullOrEmpty(FilterStatus)) p.Add("St", FilterStatus);

                    parts.Add($@"SELECT 'Mahberat' AS ReceiptType,
                        COALESCE(sr.wereda_name,'')    AS WeredaName,
                        COALESCE(sr.mahberat_name,'')  AS EntityName,
                        COALESCE(sr.driver_name,'')    AS DriverName,
                        COALESCE(sr.plate_number,'')   AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0) AS Kilogram,
                        COALESCE(sr.price,0)    AS Price,
                        ROUND(COALESCE(sr.kilogram,0)*COALESCE(sr.price,0),2) AS Total,
                        sr.status AS Status,
                        sr.registered_at AS RegisteredAt
                    FROM staff_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End {wf}{mf}{df}{sf}");
                }

                // ── outsource_receipts ─────────────────────────────────────────
                if (doOutsource)
                {
                    var wf2 = FilterWeredaId.HasValue ? " AND sr.wereda_id=@WId" : "";
                    var df2 = FilterDriverId.HasValue ? " AND sr.driver_id=@DId" : "";
                    var sf2 = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status=@St" : "";

                    parts.Add($@"SELECT 'Outsource' AS ReceiptType,
                        COALESCE(sr.wereda_name,'')   AS WeredaName,
                        COALESCE(sr.company_name,'')  AS EntityName,
                        COALESCE(sr.driver_name,'')   AS DriverName,
                        COALESCE(sr.plate_number,'')  AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0) AS Kilogram,
                        COALESCE(sr.price,0)    AS Price,
                        ROUND(COALESCE(sr.kilogram,0)*COALESCE(sr.price,0),2) AS Total,
                        sr.status AS Status,
                        sr.registered_at AS RegisteredAt
                    FROM outsource_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End {wf2}{df2}{sf2}");
                }

                // ── private_company_receipts ───────────────────────────────────
                if (doPrivate)
                {
                    var wf3 = FilterWeredaId.HasValue ? " AND sr.wereda_id=@WId" : "";
                    var df3 = FilterDriverId.HasValue ? " AND sr.driver_id=@DId" : "";
                    var sf3 = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status=@St" : "";

                    parts.Add($@"SELECT 'Private' AS ReceiptType,
                        COALESCE(sr.wereda_name,'')  AS WeredaName,
                        COALESCE(sr.company_name,'') AS EntityName,
                        COALESCE(sr.driver_name,'')  AS DriverName,
                        COALESCE(sr.plate_number,'') AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0) AS Kilogram,
                        COALESCE(sr.price,0)    AS Price,
                        ROUND(COALESCE(NULLIF(sr.total_amount,0),sr.kilogram*sr.price),2) AS Total,
                        sr.status AS Status,
                        sr.registered_at AS RegisteredAt
                    FROM private_company_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End {wf3}{df3}{sf3}");
                }

                if (parts.Count == 0) { Receipts = new(); return; }

                var sql = string.Join("\nUNION ALL\n", parts)
                        + "\nORDER BY RegisteredAt DESC";

                Receipts = db.Query<YakaReceiptRow>(sql, p).ToList();

                // ── Stats ──────────────────────────────────────────────────────
                TotalCount    = Receipts.Count;
                TotalKg       = Receipts.Sum(r => r.Kilogram);
                TotalAmount   = Receipts.Sum(r => r.Total);
                TotalApproved = Receipts.Count(r => r.Status == "Approved" || r.Status == "Billed");
                TotalPaid     = Receipts.Count(r => r.Status == "Paid");
                TotalPending  = Receipts.Count(r => r.Status == "Pending" || r.Status == "Registered");
                TotalRejected = Receipts.Count(r => r.Status == "Rejected");

                // ── Group breakdown ────────────────────────────────────────────
                GroupedData = Receipts
                    .GroupBy(r => GroupBy switch {
                        "type"     => r.ReceiptType,
                        "status"   => r.Status,
                        "mahberat" => r.EntityName,
                        "driver"   => r.DriverName,
                        _          => r.WeredaName
                    })
                    .Select(g => new YakaGroupRow {
                        GroupKey  = string.IsNullOrEmpty(g.Key) ? "— Unknown —" : g.Key,
                        Count     = g.Count(),
                        TotalKg   = g.Sum(r => r.Kilogram),
                        TotalAmt  = g.Sum(r => r.Total),
                        Approved  = g.Count(r => r.Status == "Approved" || r.Status == "Billed"),
                        Pending   = g.Count(r => r.Status == "Pending"  || r.Status == "Registered"),
                        Rejected  = g.Count(r => r.Status == "Rejected"),
                        Paid      = g.Count(r => r.Status == "Paid")
                    })
                    .OrderByDescending(g => g.TotalAmt)
                    .ToList();

                // ── Chart: daily ───────────────────────────────────────────────
                var cp = new List<string>();
                if (doMahberat)
                    cp.Add($"SELECT receipt_date AS d, kilogram AS kg, kilogram*price AS amt FROM staff_receipts WHERE receipt_date BETWEEN @Start AND @End");
                if (doOutsource)
                    cp.Add($"SELECT receipt_date AS d, kilogram AS kg, kilogram*price AS amt FROM outsource_receipts WHERE receipt_date BETWEEN @Start AND @End");
                if (doPrivate)
                    cp.Add($"SELECT receipt_date AS d, kilogram AS kg, COALESCE(NULLIF(total_amount,0),kilogram*price) AS amt FROM private_company_receipts WHERE receipt_date BETWEEN @Start AND @End");

                var csql = $@"SELECT FORMAT(d,'MM-dd') AS lbl,
                    ROUND(SUM(kg),2) AS kg, ROUND(SUM(amt),2) AS amt
                    FROM ({string.Join(" UNION ALL ", cp)}) t
                    GROUP BY d ORDER BY d ASC";

                var cr = db.Query<dynamic>(csql, new { Start = start, End = end }).ToList();
                ChartLabels = cr.Select(r => (string)(r.lbl ?? "")).ToList();
                ChartKg     = cr.Select(r => (decimal)(r.kg  ?? 0m)).ToList();
                ChartAmount = cr.Select(r => (decimal)(r.amt ?? 0m)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YakaReport.LoadData] {ex.Message}");
                ErrorMessage = $"Database error: {ex.Message}";
            }
        }
    }

    // ── View models ───────────────────────────────────────────────────────────
    public class YakaReceiptRow
    {
        public string  ReceiptType  { get; set; } = "";
        public string  WeredaName   { get; set; } = "";
        public string  EntityName   { get; set; } = "";
        public string  DriverName   { get; set; } = "";
        public string  PlateName    { get; set; } = "";
        public string  ReceiptDate  { get; set; } = "";
        public string  ReceiptTime  { get; set; } = "";
        public decimal Kilogram     { get; set; }
        public decimal Price        { get; set; }
        public decimal Total        { get; set; }
        public string  Status       { get; set; } = "";
        public DateTime RegisteredAt { get; set; }
    }

    public class YakaGroupRow
    {
        public string  GroupKey  { get; set; } = "";
        public int     Count     { get; set; }
        public decimal TotalKg   { get; set; }
        public decimal TotalAmt  { get; set; }
        public int     Approved  { get; set; }
        public int     Pending   { get; set; }
        public int     Rejected  { get; set; }
        public int     Paid      { get; set; }
    }
}
