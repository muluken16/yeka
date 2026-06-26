using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Manager
{
    public class ReportsModel : PageModel
    {
        private readonly string _cs;

        // ── Filters ──────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string? FilterType      { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterStatus    { get; set; }
        [BindProperty(SupportsGet = true)] public int?    FilterWeredaId  { get; set; }
        [BindProperty(SupportsGet = true)] public int?    FilterMahberatId{ get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterStartDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterEndDate   { get; set; }

        // ── Data ──────────────────────────────────────────────────────────────
        public List<dynamic> Receipts  { get; set; } = new();
        public List<dynamic> Weredas   { get; set; } = new();
        public List<dynamic> Mahberats { get; set; } = new();

        // ── Summary Stats ─────────────────────────────────────────────────────
        public int     TotalCount    { get; set; }
        public decimal TotalKg       { get; set; }
        public decimal TotalAmount   { get; set; }
        public int     TotalApproved { get; set; }
        public int     TotalPending  { get; set; }
        public int     TotalRejected { get; set; }
        public int     TotalPaid     { get; set; }

        // ── Chart Data ────────────────────────────────────────────────────────
        public List<ChartPoint> DailyKgChart     { get; set; } = new();
        public List<ChartPoint> DailyAmountChart { get; set; } = new();

        // ── Error ─────────────────────────────────────────────────────────────
        public string ErrorMessage { get; set; } = "";

        public ReportsModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");

            LoadDropdowns();
            LoadReceipts();
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
            int.TryParse(Request.Form["FilterWeredaId"],   out int wid); FilterWeredaId   = wid > 0 ? wid : null;
            int.TryParse(Request.Form["FilterMahberatId"], out int mid); FilterMahberatId = mid > 0 ? mid : null;

            LoadReceipts();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Type,Wereda,Mahberat/Company,Driver,Plate,Date,Time,Kilogram,Price/KG,Total,Status,Registered At");
            foreach (dynamic r in Receipts)
            {
                decimal kg    = (decimal)(r.kilogram ?? 0m);
                decimal price = (decimal)(r.price    ?? 0m);
                decimal total = (decimal)(r.total    ?? 0m);
                sb.AppendLine($"\"{r.receiptType}\",\"{r.weredaName}\",\"{r.entityName}\"," +
                              $"\"{r.driverName}\",\"{r.plateName}\",\"{r.receiptDate}\",\"{r.receiptTime}\"," +
                              $"{kg},{price},{total},\"{r.status}\",\"{r.registeredAt}\"");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"receipts_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void LoadDropdowns()
        {
            try
            {
                using var db = new MySqlConnection(_cs);
                Weredas   = db.Query<dynamic>("SELECT id, name FROM weredas   WHERE is_active = 1 ORDER BY name").ToList();
                Mahberats = db.Query<dynamic>("SELECT id, name FROM mahberats WHERE is_active = 1 ORDER BY name").ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manager.Reports] Dropdowns: {ex.Message}");
            }
        }

        private void LoadReceipts()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                // ── date range ─────────────────────────────────────────────────
                var startDate = string.IsNullOrWhiteSpace(FilterStartDate)
                    ? DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd")
                    : FilterStartDate;
                var endDate = string.IsNullOrWhiteSpace(FilterEndDate)
                    ? DateTime.Today.ToString("yyyy-MM-dd")
                    : FilterEndDate;

                var p = new DynamicParameters();
                p.Add("Start", startDate);
                p.Add("End",   endDate);

                bool doMahberat  = string.IsNullOrEmpty(FilterType) || FilterType == "All" || FilterType == "Mahberat";
                bool doOutsource = string.IsNullOrEmpty(FilterType) || FilterType == "All" || FilterType == "Outsource";
                bool doPrivate   = string.IsNullOrEmpty(FilterType) || FilterType == "All" || FilterType == "Private";

                var parts = new List<string>();

                // ════ staff_receipts (Mahberat) ════
                // Real columns: id, wereda_id, wereda_name, mahberat_id, mahberat_name,
                //   vehicle_id, plate_number, driver_id, driver_name,
                //   receipt_time, receipt_date, kilogram, price, status,
                //   notes, image_url, registered_by, registered_at,
                //   mahberat_approved, approved_by, rejected_by, transport_request_id
                if (doMahberat)
                {
                    string wf = FilterWeredaId.HasValue   ? " AND sr.wereda_id   = @WId" : "";
                    string mf = FilterMahberatId.HasValue ? " AND sr.mahberat_id = @MId" : "";
                    string sf = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status = @St" : "";
                    if (FilterWeredaId.HasValue)   p.Add("WId", FilterWeredaId.Value);
                    if (FilterMahberatId.HasValue) p.Add("MId", FilterMahberatId.Value);
                    if (!string.IsNullOrEmpty(FilterStatus)) p.Add("St", FilterStatus);

                    parts.Add($@"
                        SELECT
                            sr.id,
                            'Mahberat'                              AS receiptType,
                            COALESCE(sr.wereda_name,  '')           AS weredaName,
                            COALESCE(sr.mahberat_name,'')           AS entityName,
                            COALESCE(sr.driver_name,  '')           AS driverName,
                            COALESCE(sr.plate_number, '')           AS plateName,
                            COALESCE(sr.kilogram, 0)                AS kilogram,
                            COALESCE(sr.price,    0)                AS price,
                            ROUND(COALESCE(sr.kilogram,0) * COALESCE(sr.price,0), 2) AS total,
                            DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS receiptDate,
                            COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'), '') AS receiptTime,
                            sr.status,
                            COALESCE(sr.notes, '')                  AS notes,
                            COALESCE(sr.image_url, '')              AS imageUrl,
                            COALESCE(sr.registered_by, '')          AS registeredBy,
                            sr.registered_at                        AS registeredAt
                        FROM staff_receipts sr
                        WHERE sr.receipt_date BETWEEN @Start AND @End
                          {wf} {mf} {sf}");
                }

                // ════ outsource_receipts ════
                // Real columns: id, wereda_id, wereda_name, company_id, company_name,
                //   vehicle_id, plate_number, driver_id, driver_name,
                //   receipt_time, receipt_date, kilogram, price, status,
                //   notes, image_url, registered_by, registered_at,
                //   mahberat_approved, approved_by, rejected_by
                if (doOutsource)
                {
                    string wf2 = FilterWeredaId.HasValue   ? " AND sr.wereda_id = @WId" : "";
                    string sf2 = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status = @St" : "";

                    parts.Add($@"
                        SELECT
                            sr.id,
                            'Outsource'                              AS receiptType,
                            COALESCE(sr.wereda_name,   '')           AS weredaName,
                            COALESCE(sr.company_name,  '')           AS entityName,
                            COALESCE(sr.driver_name,   '')           AS driverName,
                            COALESCE(sr.plate_number,  '')           AS plateName,
                            COALESCE(sr.kilogram, 0)                 AS kilogram,
                            COALESCE(sr.price,    0)                 AS price,
                            ROUND(COALESCE(sr.kilogram,0) * COALESCE(sr.price,0), 2) AS total,
                            DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS receiptDate,
                            COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'), '') AS receiptTime,
                            sr.status,
                            COALESCE(sr.notes, '')                   AS notes,
                            COALESCE(sr.image_url, '')               AS imageUrl,
                            COALESCE(sr.registered_by, '')           AS registeredBy,
                            sr.registered_at                         AS registeredAt
                        FROM outsource_receipts sr
                        WHERE sr.receipt_date BETWEEN @Start AND @End
                          {wf2} {sf2}");
                }

                // ════ private_company_receipts ════
                // Real columns: id, company_id, company_name, wereda_id, wereda_name,
                //   vehicle_id, plate_number, driver_id, driver_name,
                //   receipt_time, receipt_date, kilogram, price, total_amount,
                //   notes, image_url, registered_by, status, registered_at
                if (doPrivate)
                {
                    string wf3 = FilterWeredaId.HasValue   ? " AND sr.wereda_id = @WId" : "";
                    string sf3 = !string.IsNullOrEmpty(FilterStatus) ? " AND sr.status = @St" : "";

                    parts.Add($@"
                        SELECT
                            sr.id,
                            'Private'                                AS receiptType,
                            COALESCE(sr.wereda_name,  '')            AS weredaName,
                            COALESCE(sr.company_name, '')            AS entityName,
                            COALESCE(sr.driver_name,  '')            AS driverName,
                            COALESCE(sr.plate_number, '')            AS plateName,
                            COALESCE(sr.kilogram, 0)                 AS kilogram,
                            COALESCE(sr.price,    0)                 AS price,
                            ROUND(COALESCE(NULLIF(sr.total_amount,0), sr.kilogram * sr.price), 2) AS total,
                            DATE_FORMAT(sr.receipt_date, '%Y-%m-%d') AS receiptDate,
                            COALESCE(TIME_FORMAT(sr.receipt_time, '%H:%i:%s'), '') AS receiptTime,
                            sr.status,
                            COALESCE(sr.notes, '')                   AS notes,
                            ''                                       AS imageUrl,
                            COALESCE(sr.registered_by, '')           AS registeredBy,
                            sr.registered_at                         AS registeredAt
                        FROM private_company_receipts sr
                        WHERE sr.receipt_date BETWEEN @Start AND @End
                          {wf3} {sf3}");
                }

                if (parts.Count == 0) { Receipts = new(); return; }

                var sql = string.Join("\nUNION ALL\n", parts)
                        + "\nORDER BY registeredAt DESC";

                Receipts = db.Query<dynamic>(sql, p).ToList();

                // ── Summaries ─────────────────────────────────────────────────
                TotalCount    = Receipts.Count;
                TotalKg       = Receipts.Sum(r => (decimal)(r.kilogram ?? 0m));
                TotalAmount   = Receipts.Sum(r => (decimal)(r.total    ?? 0m));
                TotalPending  = Receipts.Count(r => { var s = Convert.ToString(r.status) ?? ""; return s == "Pending"  || s == "Registered"; });
                TotalApproved = Receipts.Count(r => { var s = Convert.ToString(r.status) ?? ""; return s == "Approved" || s == "Billed"; });
                TotalPaid     = Receipts.Count(r => { var s = Convert.ToString(r.status) ?? ""; return s == "Paid"; });
                TotalRejected = Receipts.Count(r => { var s = Convert.ToString(r.status) ?? ""; return s == "Rejected" || s == "MahberatRejected"; });

                // ── Daily chart (last 30 days, same type/filter) ──────────────
                var cs = DateTime.Today.AddDays(-29).ToString("yyyy-MM-dd");
                var ce = DateTime.Today.ToString("yyyy-MM-dd");
                var cp = new DynamicParameters();
                cp.Add("CS", cs); cp.Add("CE", ce);

                var chartParts = new List<string>();
                if (doMahberat)
                    chartParts.Add("SELECT receipt_date AS d, kilogram AS kg, kilogram*price AS amt FROM staff_receipts WHERE receipt_date BETWEEN @CS AND @CE");
                if (doOutsource)
                    chartParts.Add("SELECT receipt_date AS d, kilogram AS kg, kilogram*price AS amt FROM outsource_receipts WHERE receipt_date BETWEEN @CS AND @CE");
                if (doPrivate)
                    chartParts.Add("SELECT receipt_date AS d, kilogram AS kg, COALESCE(NULLIF(total_amount,0), kilogram*price) AS amt FROM private_company_receipts WHERE receipt_date BETWEEN @CS AND @CE");

                var chartSql = $@"
                    SELECT DATE_FORMAT(d,'%m-%d') AS label,
                           ROUND(SUM(kg),2)       AS kg,
                           ROUND(SUM(amt),2)      AS amt
                    FROM ({string.Join(" UNION ALL ", chartParts)}) t
                    GROUP BY d ORDER BY d ASC";

                var rows = db.Query<dynamic>(chartSql, cp).ToList();
                DailyKgChart     = rows.Select(r => new ChartPoint { Label = r.label, Value = (decimal)(r.kg  ?? 0m) }).ToList();
                DailyAmountChart = rows.Select(r => new ChartPoint { Label = r.label, Value = (decimal)(r.amt ?? 0m) }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Manager.Reports] LoadReceipts ERROR: {ex.Message}");
                ErrorMessage = $"Database error: {ex.Message}";
            }
        }
    }

    public class ChartPoint
    {
        public string  Label { get; set; } = "";
        public decimal Value { get; set; }
    }
}
