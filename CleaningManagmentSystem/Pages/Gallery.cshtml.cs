using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;

namespace CleaningManagmentSystem.Pages;

public class GalleryModel : PageModel
{
    private readonly string _cs;

    [BindProperty(SupportsGet = true)]
    public string FilterCategory { get; set; } = "";

    public List<GalleryPhoto> Photos     { get; set; } = new();
    public List<string>       Categories { get; set; } = new();
    public int                TotalCount { get; set; }

    public GalleryModel(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

    public void OnGet()
    {
        try
        {
            using var db = new MySqlConnection(_cs);

            Categories = db.Query<string>(
                "SELECT DISTINCT category FROM gallery WHERE is_active=1 AND category IS NOT NULL AND category != '' ORDER BY category")
                .ToList();

            var where = string.IsNullOrEmpty(FilterCategory)
                ? "WHERE is_active = 1"
                : "WHERE is_active = 1 AND category = @C";

            Photos = db.Query<GalleryPhoto>(
                $@"SELECT id, title, description, image_url AS ImageUrl, category, created_at AS CreatedAt
                   FROM gallery {where}
                   ORDER BY created_at DESC",
                new { C = FilterCategory }).ToList();

            TotalCount = db.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM gallery WHERE is_active=1");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gallery.OnGet] {ex.Message}");
        }
    }
}

public class GalleryPhoto
{
    public int      Id          { get; set; }
    public string   Title       { get; set; } = "";
    public string   Description { get; set; } = "";
    public string   ImageUrl    { get; set; } = "";
    public string   Category    { get; set; } = "";
    public DateTime CreatedAt   { get; set; }
}
