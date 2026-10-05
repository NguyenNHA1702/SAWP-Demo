using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SAWP.Web.Data;
using SAWP.Web.Domain;
using SAWP.Web.Infrastructure;
using SAWP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=SAWP_Demo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<SawpDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Authentication & Cookie Setup
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".SAWP.Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddHttpContextAccessor();

// 3. Authorization & Folder-level Role Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ParentOnly", policy => policy.RequireRole(nameof(SystemRole.Parent)));
    options.AddPolicy("TeacherOnly", policy => policy.RequireRole(nameof(SystemRole.Teacher)));
    options.AddPolicy("OfficeOnly", policy => policy.RequireRole(nameof(SystemRole.Office)));
    options.AddPolicy("ServiceOnly", policy => policy.RequireRole(nameof(SystemRole.Service)));
    options.AddPolicy("BoardOnly", policy => policy.RequireRole(nameof(SystemRole.Board)));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(SystemRole.Admin)));
});

// 4. Razor Pages with Folder Conventions
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Parent", "ParentOnly");
    options.Conventions.AuthorizeFolder("/Teacher", "TeacherOnly");
    options.Conventions.AuthorizeFolder("/Office", "OfficeOnly");
    options.Conventions.AuthorizeFolder("/Service", "ServiceOnly");
    options.Conventions.AuthorizeFolder("/Board", "BoardOnly");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizePage("/Account/Profile");
    options.Conventions.AuthorizePage("/Account/ChangePassword");
    options.Conventions.AuthorizePage("/Notifications");
});

// 5. Core Services Registration
builder.Services.AddSingleton<IClock, ClockService>();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<IScopeService, ScopeService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRequestWorkflowService, RequestWorkflowService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();

var app = builder.Build();

// 6. Automatic Migration and Demo Seeding on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<SawpDbContext>();
    var passwordService = services.GetRequiredService<IPasswordService>();

    try
    {
        logger.LogInformation("[Startup] Ensuring database is created and migrated...");
        await db.Database.MigrateAsync();
        logger.LogInformation("[Startup] Database migration completed.");

        logger.LogInformation("[Startup] Running DemoSeeder...");
        await DemoSeeder.SeedAsync(db, passwordService, logger);
        logger.LogInformation("[Startup] Demo seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[Startup] Error occurred while migrating or seeding the database.");
    }
}

// 7. Request Pipeline Configuration
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
