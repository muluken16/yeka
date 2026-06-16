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
        Gallery = GetFallbackGallery();

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

    private List<dynamic> GetFallbackGallery()
    {
        var fallback = new List<dynamic>();
        var galFallback = new[] {
            ("https://images.unsplash.com/photo-1416879595882-3373a0480b5b?w=600&q=80", "City Cleaning"),
            ("https://images.unsplash.com/photo-1558618047-3c8c76ca7d13?w=600&q=80", "Waste Management"),
            ("https://images.unsplash.com/photo-1600880292203-757bb62b4baf?w=600&q=80", "Office Cleaning"),
            ("https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=600&q=80", "Team at Work"),
            ("https://images.unsplash.com/photo-1571019613454-1cb2f99b2d8b?w=600&q=80", "Equipment"),
            ("https://images.unsplash.com/photo-1504307651254-35680f356dfd?w=600&q=80", "Street Cleaning")
        };
        for (int i = 0; i < galFallback.Length; i++)
            fallback.Add(new { id = i + 1, title = galFallback[i].Item2, image_url = galFallback[i].Item1, category = "", description = "" });
        return fallback;
    }
}