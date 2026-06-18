using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.HR
{
    public class PerformanceReviewRecord
    {
        public int      Id                     { get; set; }
        public int      EmployeeId             { get; set; }
        public string   EmployeeName           { get; set; } = "";
        public string   Department             { get; set; } = "";
        public string   ReviewPeriod           { get; set; } = "";
        public decimal  KpiScore               { get; set; }
        public int      FinalRating            { get; set; }
        public decimal  GoalsAchieved          { get; set; }
        public string   PromotionRecommendation{ get; set; } = "No";
        public string   ManagerComments        { get; set; } = "";
        public string   ReviewerName           { get; set; } = "";
        public DateTime CreatedAt              { get; set; }
    }

    public class PerformanceReviewModel : PageModel
    {
        private readonly string _cs;

        public PerformanceReviewModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        public List<PerformanceReviewRecord> Reviews     { get; set; } = new();
        public List<EmployeeDto>             AllEmployees{ get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public string PeriodFilter   { get; set; } = "";
        public int?   EmpFilter      { get; set; }

        private bool IsAuthorized() =>
            HttpContext.Session.GetString("UserRole") is "hr" or "superadmin" or "manager";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet(string? period, int? empId)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            PeriodFilter = period ?? "";
            EmpFilter    = empId;

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage   = TempData["Error"]?.ToString()   ?? "";

            LoadData();
            return Page();
        }

        private void LoadData()
        {
            using var db = new MySqlConnection(_cs);

            AllEmployees = db.Query<EmployeeDto>(
                "SELECT id, first_name, last_name, department FROM employees WHERE employment_status='Active' ORDER BY first_name")
                .ToList();

            var sql = @"SELECT pr.*,
                               CONCAT(e.first_name,' ',e.last_name)  AS employee_name,
                               e.department,
                               CONCAT(rv.first_name,' ',rv.last_name) AS reviewer_name
                        FROM employee_performance_reviews pr
                        JOIN employees e  ON e.id  = pr.employee_id
                        LEFT JOIN employees rv ON rv.id = pr.reviewed_by
                        WHERE (@p='' OR pr.review_period LIKE @plike)
                          AND (@eid IS NULL OR pr.employee_id=@eid)
                        ORDER BY pr.created_at DESC";

            Reviews = db.Query<PerformanceReviewRecord>(sql,
                new { p = PeriodFilter, plike = $"%{PeriodFilter}%", eid = EmpFilter })
                .ToList();
        }

        // ── ADD ───────────────────────────────────────────────────────────────
        public IActionResult OnPostAdd(int EmployeeId, string ReviewPeriod,
            decimal KpiScore, int FinalRating, decimal GoalsAchieved,
            string PromotionRecommendation, string ManagerComments)
        {
            if (!IsAuthorized()) return RedirectToPage("/Login");

            try
            {
                var reviewerId = HttpContext.Session.GetInt32("UserId") ?? 0;
                using var db   = new MySqlConnection(_cs);

                db.Execute(@"INSERT INTO employee_performance_reviews
                    (employee_id, review_period, kpi_score, final_rating, goals_achieved,
                     promotion_recommendation, manager_comments, reviewed_by, created_at)
                    VALUES (@Eid, @Rp, @Kpi, @Fr, @Ga, @Pr, @Mc, @Rv, NOW())",
                    new
                    {
                        Eid = EmployeeId, Rp = ReviewPeriod, Kpi = KpiScore,
                        Fr  = FinalRating, Ga = GoalsAchieved,
                        Pr  = PromotionRecommendation, Mc = ManagerComments,
                        Rv  = reviewerId
                    });

                var empName = db.QueryFirstOrDefault<string>(
                    "SELECT CONCAT(first_name,' ',last_name) FROM employees WHERE id=@Id", new { Id = EmployeeId });

                TempData["Success"] = $"Performance review for {empName} ({ReviewPeriod}) saved. Rating: {FinalRating}/5.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            return RedirectToPage();
        }
    }
}
