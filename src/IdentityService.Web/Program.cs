using IdentityService.Infrastructure;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IdentitySolution.ServiceDiscovery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure ForwardedHeaders for reverse proxy support
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Clear the default networks/proxies to allow forwarded headers from any source
    // In production, you should restrict this to known proxy IPs
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/identity-service-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ==============================================================================================
// WARNING: STATIC KEY STORAGE IN FILE SYSTEM
// ==============================================================================================
// You rely on the file system for Data Protection keys. 
// Since your services run on different machines, they DO NOT share this folder contents automatically.
//
// YOU MUST MANUALLY COPY the XML key files from this 'Keys' folder to the 'Keys' folder 
// of every other machine hosting `UiService` or `UiServiceTwo`.
//
// If the keys do not match EXACTLY, Cross-App SSO will FAIL.
// ==============================================================================================
var keysFolder = Path.Combine(AppContext.BaseDirectory, "Keys");
Directory.CreateDirectory(keysFolder);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("IdentitySolution");

// Add services to the container.
Log.Information("Registering Infrastructure (RabbitMQ, Database)...");
builder.Services.AddInfrastructure(builder.Configuration);
Log.Information("Infrastructure registration completed.");

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Idsrv_SSO";
    options.Cookie.Path = "/";
    // Set domain to allow sharing across subdomains
    var cookieDomain = builder.Configuration["CookieDomain"];
    if (!string.IsNullOrEmpty(cookieDomain))
    {
        options.Cookie.Domain = cookieDomain;
    }
    // None + Always is required for modern Chrome/Edge to send cookies across different localhost ports
    options.Cookie.SameSite = SameSiteMode.None; 
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; 
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
});

// Add Consul
Log.Information("Registering Consul Services...");
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddScoped<IConsulService, ConsulService>();
builder.Services.AddHostedService<ModuleSyncWorker>();
Log.Information("Consul Services registration completed.");

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// IMPORTANT: UseForwardedHeaders must be called before other middlewares
// to ensure correct scheme/host detection behind reverse proxy
app.UseForwardedHeaders();

app.Logger.LogWarning("==============================================================================================");
app.Logger.LogWarning("WARNING: STATIC KEY STORAGE IN FILE SYSTEM");
app.Logger.LogWarning("YOU MUST MANUALLY COPY the XML key files from 'Keys' folder to all other machines.");
app.Logger.LogWarning("If the keys do not match EXACTLY, Cross-App SSO will FAIL.");
app.Logger.LogWarning("==============================================================================================");

Log.Information("Initializing Consul Middleware...");
app.UseConsul();
Log.Information("Consul Middleware initialized.");

// Seed database
try 
{
    Log.Information("Starting database seeding...");
    using (var scope = app.Services.CreateScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.SeedAsync();
    }
    Log.Information("Database seeding completed successfully.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "CRITICAL: An error occurred while seeding the database. The application may not function correctly.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Debug Middleware to see what cookies are coming in (development only)
    app.Use(async (context, next) =>
    {
        var cookies = string.Join("; ", context.Request.Cookies.Select(c => $"{c.Key}={c.Value.Substring(0, Math.Min(10, c.Value.Length))}..."));
        Console.WriteLine($"[DEBUG-CORE] Request: {context.Request.Path} | Cookies: {(string.IsNullOrEmpty(cookies) ? "NONE" : cookies)}");
        await next();
    });
    
    // Only use HTTPS redirection in development
    // In production behind a reverse proxy, the proxy handles SSL termination
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Account/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();
app.MapRazorPages();
app.MapHealthChecks("/health");


Log.Information("Application pipeline configured. Starting web host...");
app.Run();
