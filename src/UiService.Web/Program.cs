using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using MassTransit;
using IdentitySolution.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure OIDC Authentication with Cookie storage
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "SSO.Cookie";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["IdentityService:Authority"] ?? "https://localhost:7242";
    options.ClientId = "ui-client";
    options.ClientSecret = "ui-secret";
    options.ResponseType = "code";
    options.ResponseMode = "query";
    
    options.SaveTokens = true; // This ensures tokens (access, id, refresh) are stored in the authentication cookie
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
