using IdentityService.Infrastructure;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IdentitySolution.ServiceDiscovery;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Shared Data Protection with hardcoded key (DEVELOPMENT ONLY)
var keysFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IdentitySolution", "SharedKeys");
Directory.CreateDirectory(keysFolder);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("IdentitySolution")
    .ProtectKeysWithDpapi(protectToLocalMachine: true);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Idsrv_SSO";
    options.Cookie.Path = "/";
    // None + Always is required for modern Chrome/Edge to send cookies across different localhost ports
    options.Cookie.SameSite = SameSiteMode.None; 
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; 
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
});

// Add Consul
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddScoped<IConsulService, ConsulService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseConsul();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Debug Middleware to see what cookies are coming in
app.Use(async (context, next) =>
{
    var cookies = string.Join("; ", context.Request.Cookies.Select(c => $"{c.Key}={c.Value.Substring(0, Math.Min(10, c.Value.Length))}..."));
    Console.WriteLine($"[DEBUG-CORE] Request: {context.Request.Path} | Cookies: {(string.IsNullOrEmpty(cookies) ? "NONE" : cookies)}");
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();
app.MapRazorPages();
app.MapHealthChecks("/health");


app.Run();
