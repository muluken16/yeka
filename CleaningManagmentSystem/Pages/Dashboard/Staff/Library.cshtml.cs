using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Staff
{
    public class LibraryModel : PageModel
    {
        private readonly string _cs;

        // ── Filters ──────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string SearchQuery   { get; set; } = "";
        [BindProperty(SupportsGet = true)] public string FilterCategory{ get; set; } = "";
        [BindProperty(SupportsGet = true)] public string FilterStatus  { get; set; } = "";

        // ── Add / Edit fields ─────────────────────────────────────────────────
        [BindProperty] public int    EditId   { get; set; }
        [BindProperty] public string Title    { get; set; } = "";
        [BindProperty] public string Author   { get; set; } = "";
        [BindProperty] public string Category { get; set; } = "";
        [BindProperty] public string Isbn     { get; set; } = "";
        [BindProperty] public int    Quantity { get; set; } = 1;
        [BindProperty] public string Location { get; set; } = "";
        [BindProperty] public string Status   { get; set; } = "Available";

        // ── Data ──────────────────────────────────────────────────────────────
        public List<dynamic> Items      { get; set; } = new();
        public List<dynamic> Trainings  { get; set; } = new();
        public List<string>  Categories { get; set; } = new();

        // ── Stats ─────────────────────────────────────────────────────────────
        public int TotalItems     { get; set; }
        public int AvailableItems { get; set; }
        public int BorrowedItems  { get; set; }
        public int TotalTrainings { get; set; }

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        public LibraryModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            LoadData();
            return Page();
        }

        // ── POST: Add item ────────────────────────────────────────────────────
        public IActionResult OnPostAdd()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                if (string.IsNullOrWhiteSpace(Title)) throw new Exception("Title is required.");
                using var db = new MySqlConnection(_cs);
                db.Execute(@"INSERT INTO library_items
                    (title, author, category, isbn, quantity, available, location, added_date, status)
                    VALUES (@t, @a, @c, @i, @q, @q, @l, CAST(NOW() AS DATE), @s)",
                    new { t=Title, a=Author, c=Category, i=Isbn, q=Quantity, l=Location, s=Status });
                SuccessMessage = $"'{Title}' added to library.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            LoadData(); return Page();
        }

        // ── POST: Update item ─────────────────────────────────────────────────
        public IActionResult OnPostUpdate()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                if (string.IsNullOrWhiteSpace(Title)) throw new Exception("Title is required.");
                using var db = new MySqlConnection(_cs);
                db.Execute(@"UPDATE library_items SET
                    title=@t, author=@a, category=@c, isbn=@i,
                    quantity=@q, location=@l, status=@s WHERE id=@id",
                    new { t=Title, a=Author, c=Category, i=Isbn, q=Quantity, l=Location, s=Status, id=EditId });
                SuccessMessage = "Item updated.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            LoadData(); return Page();
        }

        // ── POST: Delete item ─────────────────────────────────────────────────
        public IActionResult OnPostDelete(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                db.Execute("DELETE FROM library_items WHERE id=@id", new { id });
                SuccessMessage = "Item removed.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            LoadData(); return Page();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void LoadData()
        {
            try
            {
                using var db = new MySqlConnection(_cs);

                // Distinct categories for filter
                Categories = db.Query<string>(
                    "SELECT DISTINCT category FROM library_items WHERE category IS NOT NULL AND category != '' ORDER BY category")
                    .ToList();

                // Build WHERE
                var where = new List<string>();
                var p = new DynamicParameters();

                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    where.Add("(title LIKE @S OR author LIKE @S OR category LIKE @S OR isbn LIKE @S)");
                    p.Add("S", $"%{SearchQuery}%");
                }
                if (!string.IsNullOrEmpty(FilterCategory))
                { where.Add("category = @C"); p.Add("C", FilterCategory); }
                if (!string.IsNullOrEmpty(FilterStatus))
                { where.Add("status = @St"); p.Add("St", FilterStatus); }

                var wc = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

                Items = db.Query<dynamic>(
                    $"SELECT id, title, author, category, isbn, quantity, available, location, added_date, status FROM library_items {wc} ORDER BY added_date DESC",
                    p).ToList();

                // Stats
                TotalItems     = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM library_items");
                AvailableItems = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM library_items WHERE status='Available'");
                BorrowedItems  = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM library_items WHERE status='Borrowed'");

                // Trending trainings
                var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                Trainings = db.Query<dynamic>(@"
                    SELECT id, title, trainer, location, start_date, end_date, status, category
                    FROM trainings
                    WHERE (assigned_to_user_id = @Uid OR assigned_to_user_id IS NULL)
                    ORDER BY start_date ASC", new { Uid = userId }).ToList();
                TotalTrainings = Trainings.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Library] {ex.Message}");
                ErrorMessage = $"Error loading data: {ex.Message}";
            }
        }
    }
}
