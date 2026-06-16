using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages.Dashboard.Staff
{
    public class GalleryModel : PageModel
    {
        private readonly string _cs;

        // ── Bind fields ───────────────────────────────────────────────────────
        [BindProperty] public int         EditId      { get; set; }
        [BindProperty] public string      Title       { get; set; } = "";
        [BindProperty] public string      Description { get; set; } = "";
        [BindProperty] public string      ImageUrl    { get; set; } = "";
        [BindProperty] public string      Category    { get; set; } = "";
        [BindProperty] public IFormFile?  ImageFile   { get; set; }

        // ── Filter ────────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string FilterCategory { get; set; } = "";

        // ── Data ─────────────────────────────────────────────────────────────
        public List<GalleryItem>  GalleryItems { get; set; } = new();
        public List<string>       Categories   { get; set; } = new();

        // ── Stats ─────────────────────────────────────────────────────────────
        public int TotalPhotos     { get; set; }
        public int PublishedPhotos { get; set; }
        public int TotalViews      { get; set; }

        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";

        public GalleryModel(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ── GET ───────────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            LoadData();
            return Page();
        }

        // ── POST: Add photo ───────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddAsync()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                if (string.IsNullOrWhiteSpace(Title)) throw new Exception("Title is required.");

                string? savedUrl = ImageUrl;

                // Upload image file if provided
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "gallery");
                    Directory.CreateDirectory(folder);
                    var fileName = $"gal_{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                    var path = Path.Combine(folder, fileName);
                    using var stream = new FileStream(path, FileMode.Create);
                    await ImageFile.CopyToAsync(stream);
                    savedUrl = $"/uploads/gallery/{fileName}";
                }

                using var db = new MySqlConnection(_cs);
                db.Execute(@"INSERT INTO gallery (title, description, image_url, category, views, is_active, created_at)
                             VALUES (@t, @d, @u, @c, 0, 0, NOW())",
                    new { t=Title, d=Description, u=savedUrl ?? "", c=Category });
                SuccessMessage = $"Photo '{Title}' added. Use Publish to show on home page.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            LoadData(); return Page();
        }

        // ── POST: Update photo ────────────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdateAsync()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                string? savedUrl = ImageUrl;
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "gallery");
                    Directory.CreateDirectory(folder);
                    var fileName = $"gal_{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                    var path = Path.Combine(folder, fileName);
                    using var stream = new FileStream(path, FileMode.Create);
                    await ImageFile.CopyToAsync(stream);
                    savedUrl = $"/uploads/gallery/{fileName}";
                }

                using var db = new MySqlConnection(_cs);

                if (!string.IsNullOrEmpty(savedUrl))
                    db.Execute(@"UPDATE gallery SET title=@t, description=@d, image_url=@u, category=@c WHERE id=@id",
                        new { t=Title, d=Description, u=savedUrl, c=Category, id=EditId });
                else
                    db.Execute(@"UPDATE gallery SET title=@t, description=@d, category=@c WHERE id=@id",
                        new { t=Title, d=Description, c=Category, id=EditId });

                SuccessMessage = "Photo updated.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            LoadData(); return Page();
        }

        // ── POST: Toggle publish to home page ─────────────────────────────────
        public IActionResult OnPostTogglePublish(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                var current = db.QueryFirstOrDefault<int>("SELECT is_active FROM gallery WHERE id=@id", new{id});
                var newVal = current == 1 ? 0 : 1;
                db.Execute("UPDATE gallery SET is_active=@v WHERE id=@id", new{v=newVal, id});
                SuccessMessage = newVal == 1
                    ? "Photo published to home page."
                    : "Photo removed from home page.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            LoadData(); return Page();
        }

        // ── POST: Delete photo ────────────────────────────────────────────────
        public IActionResult OnPostDelete(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                return RedirectToPage("/Login");
            try
            {
                using var db = new MySqlConnection(_cs);
                var imgUrl = db.QueryFirstOrDefault<string>("SELECT image_url FROM gallery WHERE id=@id", new{id});
                if (!string.IsNullOrEmpty(imgUrl) && imgUrl.StartsWith("/uploads/gallery/"))
                {
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                                                imgUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }
                db.Execute("DELETE FROM gallery WHERE id=@id", new{id});
                SuccessMessage = "Photo deleted.";
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
                Categories = db.Query<string>(
                    "SELECT DISTINCT category FROM gallery WHERE category IS NOT NULL AND category != '' ORDER BY category")
                    .ToList();

                var where = string.IsNullOrEmpty(FilterCategory) ? "" : "WHERE category=@C";
                GalleryItems = db.Query<GalleryItem>(
                    $"SELECT id, title, description, image_url, category, views, is_active, created_at FROM gallery {where} ORDER BY created_at DESC",
                    new { C = FilterCategory }).ToList();

                TotalPhotos     = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM gallery");
                PublishedPhotos = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM gallery WHERE is_active=1");
                TotalViews      = db.QueryFirstOrDefault<int>("SELECT COALESCE(SUM(views),0) FROM gallery");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gallery] {ex.Message}");
                ErrorMessage = $"Error: {ex.Message}";
            }
        }
    }

    public class GalleryItem
    {
        public int      Id          { get; set; }
        public string   Title       { get; set; } = "";
        public string   Description { get; set; } = "";
        public string   ImageUrl    { get; set; } = "";
        public string   Category    { get; set; } = "";
        public int      Views       { get; set; }
        public bool     IsActive    { get; set; }
        public DateTime CreatedAt   { get; set; }
    }
}
