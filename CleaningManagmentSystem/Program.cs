using System.IO;
using Microsoft.AspNetCore.Http;
using CleaningManagmentSystem.Data;
using CleaningManagmentSystem.Services;
using Dapper;

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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

try
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        await DatabaseSeeder.SeedAsync(connectionString);
    }
}
catch (Exception ex)
{
    // Log to the ASP.NET Core logger instead of stdout
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogError(ex, "Database initialization failed");
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

app.MapGet("/Dashboard/Staff", (HttpContext context) =>
{
    context.Response.Redirect("/Dashboard/Staff/Index");
    return Results.Empty;
});

app.MapRazorPages();
app.MapControllers();

// ── One-shot migration endpoint ──────────────────────────────────────────────
// Visit http://localhost:5000/run-migrations once to fix any missing columns.
// Safe to call multiple times — all statements are guarded.
app.MapGet("/run-migrations", async (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DefaultConnection") ?? "";
    using var db = new MySqlConnector.MySqlConnection(cs);
    await db.OpenAsync();

    var results = new System.Text.StringBuilder();
    results.AppendLine("Running migrations...\n");

    var migrations = new[]
    {
        ("employee_leaves", "approved_by",
         "ALTER TABLE employee_leaves ADD COLUMN approved_by INT NULL"),
        ("employee_leaves", "approved_at",
         "ALTER TABLE employee_leaves ADD COLUMN approved_at DATETIME NULL"),
    };

    foreach (var (tbl, col, sql) in migrations)
    {
        var exists  = await db.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME=@t AND COLUMN_NAME=@c",
            new { t = tbl, c = col });

        if (exists > 0)
        {
            results.AppendLine($"✓  {tbl}.{col} — already exists, skipped.");
            continue;
        }

        try
        {
            await db.ExecuteAsync(sql);
            results.AppendLine($"✓  {tbl}.{col} — added successfully.");
        }
        catch (Exception ex)
        {
            results.AppendLine($"✗  {tbl}.{col} — ERROR: {ex.Message}");
        }
    }

    results.AppendLine("\nDone. You can now use the Leave Management pages.");
    return Results.Text(results.ToString());
});

// ── Re-seed demo accounts endpoint ───────────────────────────────────────────
// Visit http://localhost:5000/reseed-accounts to insert/verify all demo accounts.
app.MapGet("/reseed-accounts", async (IConfiguration config) =>
{
    var cs = config.GetConnectionString("DefaultConnection") ?? "";
    using var db = new MySqlConnector.MySqlConnection(cs);
    await db.OpenAsync();

    var results = new System.Text.StringBuilder();
    results.AppendLine("=== Demo Account Seed ===\n");

    var accounts = new[]
    {
        ("Super Admin",       "superadmin@yeka.et", "admin123",     "superadmin"),
        ("HR Admin",          "hr@yeka.et",          "hr123",        "hr"),
        ("Operations Manager","manager@yeka.et",     "manager123",   "manager"),
        ("Staff Member",      "staff@yeka.et",       "staff123",     "staff"),
        ("Driver Vehicle 01", "driver1@yeka.et",     "driver123",    "driver"),
        ("Dispatch Officer",  "dispatch@yeka.et",    "dispatch123",  "dispatchofficer"),
        ("Wereda Mahberat",   "wereda@yeka.et",      "Wereda@123",   "wereda_mahberat"),
        ("Outsource Demo",    "outsource@yeka.et",   "outsource123", "outsource"),
        ("Private Co. Demo",  "private@yeka.et",     "private123",   "PrivateCompanyRep"),
    };

    foreach (var (name, email, password, role) in accounts)
    {
        var existing = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM users WHERE email=@e", new { e = email });

        if (existing > 0)
        {
            // Update password and ensure active — in case it was inserted with wrong password
            await db.ExecuteAsync(
                "UPDATE users SET password=@pw, is_active=1 WHERE email=@e",
                new { pw = password, e = email });
            results.AppendLine($"✓  {email} ({role}) — already exists, password refreshed.");
        }
        else
        {
            await db.ExecuteAsync(
                @"INSERT INTO users (name, email, password, role, phone, is_active, created_at, updated_at)
                  VALUES (@n, @e, @pw, @r, @ph, 1, NOW(), NOW())",
                new { n = name, e = email, pw = password, r = role, ph = "0911000000" });
            results.AppendLine($"✓  {email} ({role}) — created.");
        }
    }

    results.AppendLine("\n=== All accounts ready ===");
    results.AppendLine("\nDemo credentials:");
    results.AppendLine("  driver1@yeka.et     / driver123");
    results.AppendLine("  outsource@yeka.et   / outsource123");
    results.AppendLine("  private@yeka.et     / private123");
    results.AppendLine("  dispatch@yeka.et    / dispatch123");
    results.AppendLine("  wereda@yeka.et      / Wereda@123");
    results.AppendLine("  manager@yeka.et     / manager123");
    results.AppendLine("  staff@yeka.et       / staff123");
    results.AppendLine("  hr@yeka.et          / hr123");
    results.AppendLine("  superadmin@yeka.et  / admin123");
    return Results.Text(results.ToString());
});

app.Run();
