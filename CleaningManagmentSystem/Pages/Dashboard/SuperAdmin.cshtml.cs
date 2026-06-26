using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard
{
    public class SuperAdminModel : PageModel
    {
        private readonly string _cs;
        public SuperAdminModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── Summary counts ───────────────────────────────────────────────────
        public string UserPhoto       { get; set; } = "";
        public int TotalUsers         { get; set; }
        public int ActiveUsers        { get; set; }
        public int OutsourceCount     { get; set; }
        public int OutsourceActive    { get; set; }
        public int PrivateCount       { get; set; }
        public int PrivateActive      { get; set; }
        public int ReceiptCount       { get; set; }
        public decimal ReceiptTotal   { get; set; }
        public int PayrollCount       { get; set; }
        public decimal PayrollTotal   { get; set; }
        public decimal Capital        { get; set; }
        public decimal TotalIncome    { get; set; }
        public decimal TotalExpense   { get; set; }

        // ── Recent lists ─────────────────────────────────────────────────────
        public List<DashUser>        RecentUsers        { get; set; } = new();
        public List<DashOutsource>   RecentOutsource    { get; set; } = new();
        public List<DashPrivate>     RecentPrivate      { get; set; } = new();
        public List<DashReceipt>     RecentReceipts     { get; set; } = new();
        public List<DashPayroll>     RecentPayroll      { get; set; } = new();

        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            UserPhoto = HttpContext.Session.GetString("UserPhoto") ?? "";
            Load();
            return Page();
        }

        private void Load()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                // Users
                TotalUsers  = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM users");
                ActiveUsers = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM users WHERE is_active=1");
                RecentUsers = db.Query<DashUser>(
                    "SELECT id, name, email, COALESCE(role,'') AS role, is_active FROM users ORDER BY id DESC LIMIT 6").ToList();

                // Outsource companies
                OutsourceCount  = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM outsource_companies");
                OutsourceActive = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM outsource_companies WHERE status='Active'");
                RecentOutsource = db.Query<DashOutsource>(
                    @"SELECT id, company_name AS CompanyName,
                             COALESCE(contact_person,'') AS ContactPerson,
                             COALESCE(phone,'') AS Phone,
                             status
                      FROM outsource_companies ORDER BY id DESC LIMIT 5").ToList();

                // Private companies
                PrivateCount  = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM private_cleaning_companies");
                PrivateActive = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM private_cleaning_companies WHERE is_active=1");
                RecentPrivate = db.Query<DashPrivate>(
                    @"SELECT p.id,
                             p.company_name AS CompanyName,
                             COALESCE(p.contact_person,'') AS ContactPerson,
                             COALESCE(p.status,'') AS Status,
                             COALESCE(u.name,'— No Rep —') AS RepName
                      FROM private_cleaning_companies p
                      LEFT JOIN users u ON u.id = p.rep_user_id
                      ORDER BY p.id DESC LIMIT 5").ToList();

                // Receipts
                ReceiptCount = db.QuerySingleOrDefault<int>("SELECT COUNT(*) FROM receipts");
                ReceiptTotal = db.QuerySingleOrDefault<decimal?>(
                    "SELECT COALESCE(SUM(amount),0) FROM receipts") ?? 0m;
                RecentReceipts = db.Query<DashReceipt>(
                    @"SELECT id,
                             COALESCE(receipt_number,'') AS ReceiptNumber,
                             COALESCE(client_name,'') AS ClientName,
                             amount,
                             COALESCE(payment_method,'') AS PaymentMethod,
                             receipt_date AS ReceiptDate
                      FROM receipts ORDER BY id DESC LIMIT 5").ToList();

                // Payroll
                PayrollCount = db.QuerySingleOrDefault<int>(
                    "SELECT COUNT(*) FROM payroll WHERE status != 'Cancelled'");
                PayrollTotal = db.QuerySingleOrDefault<decimal?>(
                    "SELECT COALESCE(SUM(net_salary),0) FROM payroll WHERE status='Paid'") ?? 0m;
                RecentPayroll = db.Query<DashPayroll>(
                    @"SELECT id,
                             COALESCE(employee_name,'') AS EmployeeName,
                             net_salary AS NetSalary,
                             COALESCE(month,'') AS Month,
                             year AS Year,
                             COALESCE(status,'') AS Status
                      FROM payroll ORDER BY id DESC LIMIT 5").ToList();

                // Capital
                TotalIncome  = db.QuerySingleOrDefault<decimal?>(
                    "SELECT COALESCE(SUM(amount),0) FROM capital_transactions WHERE transaction_type='Income'") ?? 0m;
                TotalExpense = db.QuerySingleOrDefault<decimal?>(
                    "SELECT COALESCE(SUM(amount),0) FROM capital_transactions WHERE transaction_type='Expense'") ?? 0m;
                Capital = TotalIncome - TotalExpense;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SuperAdmin Dashboard] Load error: {ex.Message}");
            }
        }

        // ── DTOs ─────────────────────────────────────────────────────────────
        public class DashUser
        {
            public int    Id       { get; set; }
            public string Name     { get; set; } = "";
            public string Email    { get; set; } = "";
            public string Role     { get; set; } = "";
            public bool   IsActive { get; set; }
        }
        public class DashOutsource
        {
            public int    Id            { get; set; }
            public string CompanyName   { get; set; } = "";
            public string ContactPerson { get; set; } = "";
            public string Phone         { get; set; } = "";
            public string Status        { get; set; } = "";
        }
        public class DashPrivate
        {
            public int    Id            { get; set; }
            public string CompanyName   { get; set; } = "";
            public string ContactPerson { get; set; } = "";
            public string Status        { get; set; } = "";
            public string RepName       { get; set; } = "";
        }
        public class DashReceipt
        {
            public int       Id            { get; set; }
            public string    ReceiptNumber { get; set; } = "";
            public string    ClientName    { get; set; } = "";
            public decimal   Amount        { get; set; }
            public string    PaymentMethod { get; set; } = "";
            public DateTime? ReceiptDate   { get; set; }
        }
        public class DashPayroll
        {
            public int     Id           { get; set; }
            public string  EmployeeName { get; set; } = "";
            public decimal NetSalary    { get; set; }
            public string  Month        { get; set; } = "";
            public int     Year         { get; set; }
            public string  Status       { get; set; } = "";
        }
    }
}
