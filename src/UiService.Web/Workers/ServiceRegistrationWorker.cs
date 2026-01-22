using IdentitySolution.ServiceDiscovery;
using IdentitySolution.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UiService.Web.Workers;

public class ServiceRegistrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceRegistrationWorker> _logger;

    public ServiceRegistrationWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ServiceRegistrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServiceRegistrationWorker starting...");

        // Define module-specific data to be seeded in IdentityService
        var roles = new List<RoleDto>
        {
            new RoleDto { Name = "UiManager", Description = "Manager for UI Service" }
        };

        var permissions = new List<PermissionDto>
        {
            new PermissionDto { Name = "ui.view.dashboard", Module = "UI", Description = "View UI Dashboard" },
            new PermissionDto { Name = "ui.manage.settings", Module = "UI", Description = "Manage UI Settings" }
        };

        var users = new List<UserDto>
        {
            new UserDto { UserName = "uisupervisor", Email = "supervisor@ui.com", FullName = "UI Supervisor" }
        };

        var configSection = _configuration.GetSection("IdentityClient");
        var clientId = configSection["ClientId"] ?? "ui-client";
        var clientSecret = configSection["ClientSecret"] ?? "ui-secret";
        var baseUrl = configSection["BaseUrl"] ?? "https://localhost:7150";

        var oidcClients = new List<OidcClientDto>
        {
            new OidcClientDto
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                DisplayName = _configuration["ServiceName"] ?? "Main UI Service",
                RedirectUris = { $"{baseUrl}/signin-oidc" },
                PostLogoutRedirectUris = { $"{baseUrl}/signout-callback-oidc" },
                HealthCheckUrl = $"{baseUrl}/health"
            }
        };

        // Retry until successful (IdentityService might be down)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var registrationService = scope.ServiceProvider.GetRequiredService<IModuleRegistrationService>();
                    await registrationService.RegisterAsync(roles, permissions, users, oidcClients);
                }
                
                _logger.LogInformation("Module registration data sent successfully.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to send module registration data. Retrying in 10s... Error: {Message}", ex.Message);
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
