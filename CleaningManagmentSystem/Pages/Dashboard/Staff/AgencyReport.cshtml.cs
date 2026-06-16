using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Staff
{
    public class AgencyReportModel : PageModel
    {
        private readonly string _cs;

        // ── Filters ──────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string FilterType      { get; set; } = "All";
        [BindProperty(SupportsGet = true)] public string FilterStatus    { get; set; } = "";
        [BindProperty(SupportsGet = true)] public int?   FilterWeredaId  { get; set; }
        [BindProperty(SupportsGet = true)] public int?   FilterMahberatId{ get; set; }
        [BindProperty(SupportsGet = true)] public string FilterStartDate { get; set; } = "";
        [BindProperty(SupportsGet = true)] public string FilterEndDate   { get; set; } = "";

        // ── Dropdowns ─────────────────────────────────────────────────────────
        public List<dynamic> Weredas   { get; set; } = new();
        public List<dynamic> Mahberats { get; set; } = new();

        // ── Real receipt data ─────────────────────────────────────────────────
        public List<ReceiptReportRow> Receipts { get; set; } = new();

        // ── Summary stats ─────────────────────────────────────────────────────
        public int     TotalCount    { get; set; }
        public decimal TotalKg       { get; set; }
        public decimal TotalAmount   { get; set; }
        public int     TotalApproved { get; set; }
        public int     TotalPending  { get; set; }
        public int     TotalRejected { get; set; }
        public int     TotalPaid     { get; set; }

        // ── Chart data ────────────────────────────────────────────────────────
        public List<string>  ChartLabels { get; set; } = new();
        public List<decimal> ChartKg     { get; set; } = new();
        public List<decimal> ChartAmount { get; set; } = new();

        // ── Group view ────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string GroupBy { get; set; } = "wereda";

        public string ErrorMessage { get; set; } = "";

        public AgencyReportModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            // Default date range: last 30 days
            if (string.IsNullOrEmpty(FilterStartDate))
                FilterStartDate = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(FilterEndDate))
                FilterEndDate = DateTime.Today.ToString("yyyy-MM-dd");

            LoadDropdowns();
            LoadData();
            return Page();
        }

        // ── Export CSV handler ────────────────────────────────────────────────
        public IActionResult OnPostExportCsv()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            FilterType       = Request.Form["FilterType"];
            FilterStatus     = Request.Form["FilterStatus"];
            FilterStartDate  = Request.Form["FilterStartDate"];
            FilterEndDate    = Request.Form["FilterEndDate"];
            int.TryParse(Request.Form["FilterWeredaId"],   out int w); FilterWeredaId   = w > 0 ? w : null;
            int.TryParse(Request.Form["FilterMahberatId"], out int m); FilterMahberatId = m > 0 ? m : null;
            GroupBy = Request.Form["GroupBy"];

            LoadData();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Type,Wereda,Mahberat/Company,Driver,Plate,Date,Time,Kilogram,Rate,Total,Status");
            foreach (var r in Receipts)
                sb.AppendLine($"\"{r.ReceiptType}\",\"{r.WeredaName}\",\"{r.EntityName}\"," +
                              $"\"{r.DriverName}\",\"{r.PlateName}\",\"{r.ReceiptDate}\"," +
                              $"\"{r.ReceiptTime}\",{r.Kilogram},{r.Price},{r.Total},\"{r.Status}\"");

            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
                        "text/csv",
                        $"receipts_report_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        private void LoadDropdowns()
        {
            try
            {
                using var db = new MySqlConnection(_cs);
                Weredas   = db.Query<dynamic>("SELECT id, name FROM weredas WHERE is_active=1 ORDER BY name").ToList();
                Mahberats = db.Query<dynamic>("SELECT id, name FROM mahberats WHERE is_active=1 ORDER BY name").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgencyReport.LoadDropdowns] {ex.Message}");
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

                // ── staff_receipts (Mahberat) ─────────────────────────────────
                if (doMahberat)
                {
                    string wf = FilterWeredaId.HasValue   ? " AND sr.wereda_id=@WId"   : "";
                    string mf = FilterMahberatId.HasValue ? " AND sr.mahberat_id=@MId" : "";
                    string sf = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status=@St" : "";
                    if (FilterWeredaId.HasValue)   p.Add("WId", FilterWeredaId.Value);
                    if (FilterMahberatId.HasValue) p.Add("MId", FilterMahberatId.Value);
                    if (!string.IsNullOrEmpty(FilterStatus)) p.Add("St", FilterStatus);

                    parts.Add($@"SELECT 'Mahberat' AS ReceiptType,
                        COALESCE(sr.wereda_name,'')   AS WeredaName,
                        COALESCE(sr.mahberat_name,'') AS EntityName,
                        COALESCE(sr.driver_name,'')   AS DriverName,
                        COALESCE(sr.plate_number,'')  AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0)        AS Kilogram,
                        COALESCE(sr.price,0)           AS Price,
                        ROUND(COALESCE(sr.kilogram,0)*COALESCE(sr.price,0),2) AS Total,
                        sr.status AS Status,
                        sr.registered_at AS RegisteredAt
                    FROM staff_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End {wf}{mf}{sf}");
                }

                // ── outsource_receipts ────────────────────────────────────────
                if (doOutsource)
                {
                    string wf2 = FilterWeredaId.HasValue ? " AND sr.wereda_id=@WId" : "";
                    string sf2 = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status=@St" : "";

                    parts.Add($@"SELECT 'Outsource' AS ReceiptType,
                        COALESCE(sr.wereda_name,'')  AS WeredaName,
                        COALESCE(sr.company_name,'') AS EntityName,
                        COALESCE(sr.driver_name,'')  AS DriverName,
                        COALESCE(sr.plate_number,'') AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0)       AS Kilogram,
                        COALESCE(sr.price,0)          AS Price,
                        ROUND(COALESCE(sr.kilogram,0)*COALESCE(sr.price,0),2) AS Total,
                        sr.status AS Status,
                        sr.registered_at AS RegisteredAt
                    FROM outsource_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End {wf2}{sf2}");
                }

                // ── private_company_receipts ──────────────────────────────────
                if (doPrivate)
                {
                    string wf3 = FilterWeredaId.HasValue ? " AND sr.wereda_id=@WId" : "";
                    string sf3 = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status=@St" : "";

                    parts.Add($@"SELECT 'Private' AS ReceiptType,
                        COALESCE(sr.wereda_name,'')  AS WeredaName,
                        COALESCE(sr.company_name,'') AS EntityName,
                        COALESCE(sr.driver_name,'')  AS DriverName,
                        COALESCE(sr.plate_number,'') AS PlateName,
                        DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS ReceiptDate,
                        COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'),'') AS ReceiptTime,
                        COALESCE(sr.kilogram,0)       AS Kilogram,
                        COALESCE(sr.price,0)          AS Price,
                        ROUND(COALESCE(NULLIF(sr.total_amount,0),sr.kilogram*sr.price),2) AS Total,
                        sr.status AS Status,
                        sr.registered_at AS RegisteredAt
                    FROM private_company_receipts sr
                    WHERE sr.receipt_date BETWEEN @Start AND @End {wf3}{sf3}");
                }

                if (parts.Count == 0) { Receipts = new(); return; }

                var sql = string.Join("\nUNION ALL\n", parts) + "\nORDER BY RegisteredAt DESC";
                Receipts = db.Query<ReceiptReportRow>(sql, p).ToList();

                // ── Summaries ──────────────────────────────────────────────────
                TotalCount    = Receipts.Count;
                TotalKg       = Receipts.Sum(r => r.Kilogram);
                TotalAmount   = Receipts.Sum(r => r.Total);
                TotalApproved = Receipts.Count(r => r.Status == "Approved" || r.Status == "Billed");
                TotalPaid     = Receipts.Count(r => r.Status == "Paid");
                TotalPending  = Receipts.Count(r => r.Status == "Pending" || r.Status == "Registered");
                TotalRejected = Receipts.Count(r => r.Status == "Rejected");

                // ── Chart: daily for last 30 days ──────────────────────────────
                var chartParts = new List<string>();
                if (doMahberat)
                    chartParts.Add($"SELECT receipt_date AS d, kilogram AS kg, kilogram*price AS amt FROM staff_receipts WHERE receipt_date BETWEEN @Start AND @End");
                if (doOutsource)
                    chartParts.Add($"SELECT receipt_date AS d, kilogram AS kg, kilogram*price AS amt FROM outsource_receipts WHERE receipt_date BETWEEN @Start AND @End");
                if (doPrivate)
                    chartParts.Add($"SELECT receipt_date AS d, kilogram AS kg, COALESCE(NULLIF(total_amount,0),kilogram*price) AS amt FROM private_company_receipts WHERE receipt_date BETWEEN @Start AND @End");

                var chartSql = $@"SELECT FORMAT(d,'MM-dd') AS lbl,
                    ROUND(SUM(kg),2) AS kg, ROUND(SUM(amt),2) AS amt
                    FROM ({string.Join(" UNION ALL ", chartParts)}) t
                    GROUP BY d ORDER BY d ASC";

                var chartRows = db.Query<dynamic>(chartSql, new { Start = start, End = end }).ToList();
                ChartLabels = chartRows.Select(r => (string)(r.lbl ?? "")).ToList();
                ChartKg     = chartRows.Select(r => (decimal)(r.kg  ?? 0m)).ToList();
                ChartAmount = chartRows.Select(r => (decimal)(r.amt ?? 0m)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AgencyReport.LoadData] {ex.Message}");
                ErrorMessage = $"Database error: {ex.Message}";
            }
        }
    }

    // ── View model ────────────────────────────────────────────────────────────
    public class ReceiptReportRow
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
}
