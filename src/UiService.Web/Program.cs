using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using MassTransit;
using MassTransit;
using IdentitySolution.ServiceDiscovery;
using Serilog;
using UiService.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ui-service-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Static Key Storage in File System
var keysFolder = Path.Combine(AppContext.BaseDirectory, "Keys");
Directory.CreateDirectory(keysFolder);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("IdentitySolution");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// Configure OIDC Authentication with Cookie storage
string identityAuthority = null;
try
{
    identityAuthority = await IdentitySolution.ServiceDiscovery.ServiceDiscoveryHelper.GetServiceAddressAsync(builder.Configuration, "IdentityService");
    if (!string.IsNullOrEmpty(identityAuthority))
    {
        Log.Information($"Resolved IdentityService Authority from Consul: {identityAuthority}");
    }
    else
    {
        Log.Warning("Could not resolve IdentityService from Consul. Authority will default to localhost.");
    }
}
catch (Exception ex)
{
    Log.Warning($"Failed to resolve IdentityService from Consul during startup: {ex.Message}. Authority will default to localhost.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "UiService_SSO";
    var cookieDomain = builder.Configuration["CookieDomain"];
    if (!string.IsNullOrEmpty(cookieDomain))
    {
        options.Cookie.Domain = cookieDomain;
    }
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None; 
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = identityAuthority ?? "https://localhost:7242";
    options.ClientId = builder.Configuration["IdentityClient:ClientId"] ?? "ui-client";
    options.ClientSecret = builder.Configuration["IdentityClient:ClientSecret"] ?? "ui-secret";
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

// Register Status
builder.Services.AddSingleton<UiService.Web.Services.StartupStatus>();

var app = builder.Build();

// Startup Blocker Middleware
app.Use(async (context, next) =>
{
    var status = context.RequestServices.GetService<UiService.Web.Services.StartupStatus>();
    if (status != null && !status.IsReady && !context.Request.Path.Value.StartsWith("/health"))
    {
        context.Response.StatusCode = 503;
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(@"
            <html>
            <head><meta http-equiv='refresh' content='3'></head>
            <body>
                <h1>Service initializing...</h1>
                <p>Waiting for Identity Service to become available. This page will refresh automatically.</p>
            </body>
            </html>");
        return;
    }
    await next();
});

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
    pattern: "{controller=Home}/{action=Dashboard}/{id?}");

// Start background wait
app.StartWaitForIdentityServiceInBackground();

app.Run();
