using System.IO;
using Microsoft.AspNetCore.Http;
using CleaningManagmentSystem.Data;
using CleaningManagmentSystem.Services;

var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = exeDir,
});

builder.Configuration.AddJsonFile(Path.Combine(exeDir, "appsettings.json"), optional: false);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Yeka.Session";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

Console.WriteLine($"[Debug] Current dir: {Environment.CurrentDirectory}");
Console.WriteLine($"[Debug] ContentRootPath: {AppContext.BaseDirectory}");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"[Debug] ConnectionString: {connectionString ?? "NULL"}");

Console.WriteLine("[Startup] Initializing database...");
try
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        await DatabaseSeeder.SeedAsync(connectionString);
        Console.WriteLine("[Startup] Database initialization completed");
    }
    else
    {
        Console.WriteLine("[Startup] Warning: DefaultConnection not configured");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Startup] Database initialization error: {ex.Message}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseSession();

app.Use(async (context, next) =>
{
    var userId = context.Session.GetInt32("UserId");
    var userName = context.Session.GetString("UserName");
    Console.WriteLine($"[Request] {context.Request.Path} - UserId: {userId}, UserName: {userName}");
    await next();
});

app.UseAuthorization();

app.MapGet("/Dashboard/Staff", (HttpContext context) =>
{
    context.Response.Redirect("/Dashboard/Staff/Index");
    return Results.Empty;
});

app.MapRazorPages();
app.MapControllers();

Console.WriteLine($"[Startup] Application running on http://0.0.0.0:{port} (all interfaces)");
app.Run();