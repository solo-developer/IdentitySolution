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
    options.Cookie.Name = "SSO.Cookie.Two"; // Distinct cookie name
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = builder.Configuration["IdentityService:Authority"] ?? "https://localhost:7242";
    options.ClientId = "ui-client-2"; // Distinct client id
    options.ClientSecret = "ui-secret-2";
    options.ResponseType = "code";
    options.ResponseMode = "query";
    
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
    x.AddConsumer<UiServiceTwo.Web.Consumers.UserLoggedOutConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        cfg.Host(rabbitMqHost);
        cfg.ConfigureEndpoints(context);
    });
});

// Configure Service Discovery
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddHostedService<UiServiceTwo.Web.Workers.ServiceRegistrationWorker>();

// Custom Session Management
builder.Services.AddSingleton<UiServiceTwo.Web.Services.IGlobalSessionStore, UiServiceTwo.Web.Services.GlobalSessionStore>();

var app = builder.Build();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
