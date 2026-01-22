using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentitySolution.Shared.Events;
using IdentitySolution.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace IdentityService.Infrastructure.Consumers;

public class RegisterModuleConsumer : IConsumer<IRegisterModule>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<RegisterModuleConsumer> _logger;

    public RegisterModuleConsumer(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context,
        IOpenIddictApplicationManager applicationManager,
        ILogger<RegisterModuleConsumer> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _applicationManager = applicationManager;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IRegisterModule> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing registration for module: {ModuleName}", message.ModuleName);

        // 1. Process Permissions
        foreach (var pReq in message.Permissions)
        {
            if (!await _context.Permissions.AnyAsync(p => p.Name == pReq.Name))
            {
                _context.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = pReq.Name,
                    Module = pReq.Module,
                    Description = pReq.Description
                });
            }
        }
        await _context.SaveChangesAsync();

        // 2. Process Roles
        foreach (var rReq in message.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(rReq.Name))
            {
                await _roleManager.CreateAsync(new ApplicationRole
                {
                    Name = rReq.Name,
                    Description = rReq.Description
                });
            }
        }

        // 3. Process Users (Idempotent insertion)
        foreach (var uReq in message.Users)
        {
            var user = await _userManager.FindByNameAsync(uReq.UserName);
            if (user == null)
            {
                _logger.LogInformation("Seeding new user: {UserName} for module {ModuleName}", uReq.UserName, message.ModuleName);
                var newUser = new ApplicationUser
                {
                    UserName = uReq.UserName,
                    Email = uReq.Email,
                    FullName = uReq.FullName,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(newUser, "DefaultPassword123!");
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to create user {UserName}: {Errors}", uReq.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // 4. Process OIDC Clients
        foreach (var clientReq in message.OidcClients)
        {
            if (await _applicationManager.FindByClientIdAsync(clientReq.ClientId) == null)
            {
                _logger.LogInformation("Registering new OIDC client: {ClientId} for module {ModuleName}", clientReq.ClientId, message.ModuleName);
                
                var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
                {
                    ClientId = clientReq.ClientId,
                    ClientSecret = clientReq.ClientSecret,
                    DisplayName = clientReq.DisplayName,
                    ClientType = OpenIddict.Abstractions.OpenIddictConstants.ClientTypes.Confidential,
                    Permissions =
                    {
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Logout,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Email,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Profile,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Roles,
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                        OpenIddict.Abstractions.OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                    }
                };

                foreach (var uri in clientReq.RedirectUris) descriptor.RedirectUris.Add(new Uri(uri));
                foreach (var uri in clientReq.PostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

                await _applicationManager.CreateAsync(descriptor);
            }
        }
    }
}
