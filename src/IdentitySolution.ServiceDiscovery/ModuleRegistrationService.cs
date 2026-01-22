using IdentitySolution.Shared.Events;
using IdentitySolution.Shared.Models;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentitySolution.ServiceDiscovery;

public interface IModuleRegistrationService
{
    Task RegisterAsync(List<RoleDto> roles, List<PermissionDto> permissions, List<UserDto> users, List<OidcClientDto> oidcClients);
}

public class ModuleRegistrationService : IModuleRegistrationService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModuleRegistrationService> _logger;

    public ModuleRegistrationService(
        IPublishEndpoint publishEndpoint,
        IConfiguration configuration,
        ILogger<ModuleRegistrationService> logger)
    {
        _publishEndpoint = publishEndpoint;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RegisterAsync(List<RoleDto> roles, List<PermissionDto> permissions, List<UserDto> users, List<OidcClientDto> oidcClients)
    {
        var moduleName = _configuration["ServiceName"] ?? "UnknownModule";
        _logger.LogInformation("Sending registration data for module: {ModuleName}", moduleName);

        await _publishEndpoint.Publish<IRegisterModule>(new
        {
            ModuleName = moduleName,
            Roles = roles,
            Permissions = permissions,
            Users = users,
            OidcClients = oidcClients
        });
    }
}
