using IdentitySolution.ServiceDiscovery;
using IdentitySolution.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UiServiceTwo.Web.Workers;

public class ServiceRegistrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceRegistrationWorker> _logger;

    public ServiceRegistrationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceRegistrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServiceRegistrationWorker starting...");

        // Define module-specific data to be seeded in IdentityService
        var roles = new List<RoleDto>
        {
            new RoleDto { Name = "UiManagerTwo", Description = "Manager for UI Service Two" }
        };

        var permissions = new List<PermissionDto>
        {
            new PermissionDto { Name = "ui2.view.dashboard", Module = "UI2", Description = "View UI Two Dashboard" }
        };

        var users = new List<UserDto>
        {
            new UserDto { UserName = "ui2supervisor", Email = "supervisor@ui2.com", FullName = "UI Two Supervisor" }
        };

        var oidcClients = new List<OidcClientDto>
        {
            new OidcClientDto
            {
                ClientId = "ui-client-2",
                ClientSecret = "ui-secret-2",
                DisplayName = "Second UI Service",
                RedirectUris = { "https://localhost:7160/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:7160/signout-callback-oidc" },
                FrontChannelLogoutUri = "https://localhost:7160/signout-oidc"
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
