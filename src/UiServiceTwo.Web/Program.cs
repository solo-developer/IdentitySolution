using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using MassTransit;
using MassTransit;
using IdentitySolution.ServiceDiscovery;
using Serilog;
using UiServiceTwo.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ui-service-two-.txt", rollingInterval: RollingInterval.Day)
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
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "UiServiceTwo_SSO";
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
    options.Authority = builder.Configuration["IdentityService:Authority"] ?? "https://localhost:7242";
    options.ClientId = "ui-client-2"; // Distinct client id
    options.ClientSecret = "ui-secret-2";
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

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.ContainsKey("prompt"))
            {
                context.ProtocolMessage.Prompt = context.Properties.Items["prompt"];
            }
            return Task.CompletedTask;
        }
    };
});

// Configure Messaging (MassTransit)
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<UiServiceTwo.Web.Consumers.UserLoggedOutConsumer>();
    x.AddConsumer<UiServiceTwo.Web.Consumers.UserLoggedInConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        cfg.Host(rabbitMqHost);
        cfg.ConfigureEndpoints(context);
    });
});

// Configure Service Discovery
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddScoped<IdentitySolution.ServiceDiscovery.IModuleRegistrationService, IdentitySolution.ServiceDiscovery.ModuleRegistrationService>();
builder.Services.AddHostedService<UiServiceTwo.Web.Workers.ServiceRegistrationWorker>();

// Custom Session Management
builder.Services.AddSingleton<UiServiceTwo.Web.Services.IGlobalSessionStore, UiServiceTwo.Web.Services.GlobalSessionStore>();

builder.Services.AddSignalR();

// Register Status
builder.Services.AddSingleton<UiServiceTwo.Web.Services.StartupStatus>();

var app = builder.Build();

// Startup Blocker Middleware
app.Use(async (context, next) =>
{
    var status = context.RequestServices.GetService<UiServiceTwo.Web.Services.StartupStatus>();
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

app.UseConsul();

// Custom Global Logout Check
app.UseMiddleware<UiServiceTwo.Web.Middleware.GlobalLogoutMiddleware>();

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
app.MapHub<UiServiceTwo.Web.Hubs.NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Start background wait
app.StartWaitForIdentityServiceInBackground();

app.Run();
