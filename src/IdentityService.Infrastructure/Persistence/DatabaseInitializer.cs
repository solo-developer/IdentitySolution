using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Infrastructure.Persistence;

public class DatabaseInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public DatabaseInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
    }

    public async Task SeedAsync()
    {
        // In a real microservice, you might want to handle migrations differently (e.g., via a separate tool or init weight)
        // For this demo, we'll apply them on startup
        try 
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.MigrateAsync();
            }
        }
        catch (Exception)
        {
            // Log error
        }

        await SeedRolesAndPermissionsAsync();
        await SeedAdminUserAsync();
        await SeedOidcScopesAsync();
        await SeedOidcClientsAsync();
    }

    private async Task SeedRolesAndPermissionsAsync()
    {
        // Add Permissions using reflection to get all constants
        var allPermissions = typeof(Permissions)
            .GetNestedTypes()
            .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy))
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .ToList();

        foreach (var pName in allPermissions)
        {
            if (!await _context.Permissions.AnyAsync(p => p.Name == pName))
            {
                _context.Permissions.Add(new Permission 
                { 
                    Id = Guid.NewGuid(),
                    Name = pName!, 
                    Module = pName!.Split('.')[1],
                    Description = $"Permission for {pName}"
                });
            }
        }
        await _context.SaveChangesAsync();

        // Create Admin Role
        if (!await _roleManager.RoleExistsAsync(Roles.Administrator))
        {
            var adminRole = new ApplicationRole { Name = Roles.Administrator, Description = "Full access to the system" };
            await _roleManager.CreateAsync(adminRole);

            // Assign all permissions to Admin
            var dbPermissions = await _context.Permissions.ToListAsync();
            foreach (var permission in dbPermissions)
            {
                if (!await _context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id))
                {
                    _context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permission.Id });
                }
            }
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedAdminUserAsync()
    {
        if (await _userManager.FindByNameAsync("admin") == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@identity.com",
                FullName = "System Administrator",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(adminUser, "Password123!");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, Roles.Administrator);
            }
        }
    }

    private async Task SeedOidcScopesAsync()
    {
        await EnsureScopeAsync("api", "API access", new[] { "identity-server" });
        await EnsureScopeAsync("openid", "OpenID Connect", null);
        await EnsureScopeAsync("profile", "User profile", null);
        await EnsureScopeAsync("email", "User email", null);
        await EnsureScopeAsync("roles", "User roles", null);
    }

    private async Task EnsureScopeAsync(string name, string displayName, string[]? resources = null)
    {
        if (await _scopeManager.FindByNameAsync(name) == null)
        {
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = name,
                DisplayName = displayName
            };

            if (resources != null)
            {
                foreach (var resource in resources)
                {
                    descriptor.Resources.Add(resource);
                }
            }

            await _scopeManager.CreateAsync(descriptor);
        }
    }

    private async Task SeedOidcClientsAsync()
    {
        // Define Clients
        var clients = new List<OpenIddictApplicationDescriptor>
        {
            new OpenIddictApplicationDescriptor
            {
                ClientId = "hospital-app",
                ClientSecret = "hospital-secret",
                DisplayName = "Hospital System",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                PostLogoutRedirectUris = { new Uri("https://localhost:5001/signout-callback-oidc") },
                RedirectUris = { new Uri("https://localhost:5001/signin-oidc") },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            },
            new OpenIddictApplicationDescriptor
            {
                ClientId = "financial-app",
                ClientSecret = "financial-secret",
                DisplayName = "Financial Analysis System",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                PostLogoutRedirectUris = { new Uri("https://localhost:5002/signout-callback-oidc") },
                RedirectUris = { new Uri("https://localhost:5002/signin-oidc") },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            },
            new OpenIddictApplicationDescriptor
            {
                ClientId = "ui-client",
                ClientSecret = "ui-secret",
                DisplayName = "Main UI Service",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                PostLogoutRedirectUris = { new Uri("https://localhost:7150/signout-callback-oidc") },
                RedirectUris = { new Uri("https://localhost:7150/signin-oidc") },
                FrontChannelLogoutUri = new Uri("https://localhost:7150/signout-oidc"),
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            },
            new OpenIddictApplicationDescriptor
            {
                ClientId = "ui-client-2",
                ClientSecret = "ui-secret-2",
                DisplayName = "Second UI Service",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                PostLogoutRedirectUris = { new Uri("https://localhost:7160/signout-callback-oidc") },
                RedirectUris = { new Uri("https://localhost:7160/signin-oidc") },
                FrontChannelLogoutUri = new Uri("https://localhost:7160/signout-oidc"),
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            },
            new OpenIddictApplicationDescriptor
            {
                ClientId = "recovery-project",
                ClientSecret = "recovery-secret",
                DisplayName = "Recovery Project Service",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            }
        };

        foreach (var descriptor in clients)
        {
            var application = await _applicationManager.FindByClientIdAsync(descriptor.ClientId!);
            if (application == null)
            {
                await _applicationManager.CreateAsync(descriptor);
            }
            else
            {
                await _applicationManager.UpdateAsync(application, descriptor);
            }
        }
    }
}
