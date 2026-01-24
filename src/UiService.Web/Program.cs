using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using MassTransit;
using IdentitySolution.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);

// Shared Data Protection with hardcoded key (DEVELOPMENT ONLY)
var keysFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IdentitySolution", "SharedKeys");
Directory.CreateDirectory(keysFolder);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("IdentitySolution")
    .ProtectKeysWithDpapi(protectToLocalMachine: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// Configure OIDC Authentication with Cookie storage
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "UiService_SSO";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None; 
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["IdentityService:Authority"] ?? "https://localhost:7242";
    options.ClientId = "ui-client";
    options.ClientSecret = "ui-secret";
    options.ResponseType = "code";
    options.ResponseMode = "query";
    options.RequireHttpsMetadata = false;
    
    // Explicitly set these for Edge/Chrome compatibility
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.NonceCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("roles");

    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        NameClaimType = "name",
        RoleClaimType = "role"
    };

    // Support for Single Logout
    options.SignedOutCallbackPath = "/signout-callback-oidc";
});

// Configure Messaging (MassTransit)
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<UiService.Web.Consumers.UserLoggedOutConsumer>();
    x.AddConsumer<UiService.Web.Consumers.UserLoggedInConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        cfg.Host(rabbitMqHost);
        cfg.ConfigureEndpoints(context);
    });
});

// Configure Service Discovery & Data Seeding
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddScoped<IdentitySolution.ServiceDiscovery.IModuleRegistrationService, IdentitySolution.ServiceDiscovery.ModuleRegistrationService>();
builder.Services.AddHostedService<UiService.Web.Workers.ServiceRegistrationWorker>();

// Custom Session Management
builder.Services.AddSingleton<UiService.Web.Services.IGlobalSessionStore, UiService.Web.Services.GlobalSessionStore>();

builder.Services.AddSignalR();

var app = builder.Build();

// Use Service Discovery
app.UseConsul();

// Custom Global Logout Check
app.UseMiddleware<UiService.Web.Middleware.GlobalLogoutMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHub<UiService.Web.Hubs.NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
