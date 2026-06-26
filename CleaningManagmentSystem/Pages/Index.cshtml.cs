using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Dapper;
using System.Threading.Tasks;

namespace CleaningManagmentSystem.Pages;

public class IndexModel : PageModel
{
    private readonly string _cs;

    public List<dynamic> Posts    { get; set; } = new();
    public List<dynamic> Services { get; set; } = new();
    public List<dynamic> Gallery  { get; set; } = new();

    public IndexModel(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

    public async Task OnGetAsync()
    {
        Posts = GetFallbackPosts();
        Services = GetFallbackServices();
        Gallery = new List<dynamic>();

        try
        {
            using var db = new MySqlConnection(_cs);
            await db.OpenAsync();

            Posts = (await db.QueryAsync<dynamic>(@"
                SELECT id, title, category, content,
                       COALESCE(is_pinned,0) AS is_pinned,
                       COALESCE(priority,'Normal') AS priority,
                       created_at
                FROM posts
                WHERE status = 'Published'
                ORDER BY is_pinned DESC, created_at DESC")).ToList();

            Services = (await db.QueryAsync<dynamic>(@"
                SELECT id, name, description
                FROM services
                ORDER BY id ASC")).ToList();

            Gallery = (await db.QueryAsync<dynamic>(@"
                SELECT id, title, description, image_url, category
                FROM gallery
                WHERE is_active = 1
                ORDER BY created_at DESC")).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Index.OnGet] {ex.Message}");
        }
    }

    private List<dynamic> GetFallbackPosts() => new();

    private List<dynamic> GetFallbackServices()
    {
        var fallback = new List<dynamic>
        {
            new { id = 1, name = "Residential Cleaning", description = "Regular and deep cleaning for homes and apartments." },
            new { id = 2, name = "Commercial Cleaning", description = "Office, retail space and commercial premises." },
            new { id = 3, name = "Waste Collection", description = "Organized waste collection across all weredas." },
            new { id = 4, name = "Transport Services", description = "Managed transport with driver assignment and tracking." }
        };
        return fallback;
    }
}