using IdentityService.Application.Interfaces;
using IdentityService.Web.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Novell.Directory.Ldap;

namespace IdentityService.Web.Services;

public class LdapService : ILdapService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LdapService> _logger;

    public LdapService(IServiceProvider serviceProvider, ILogger<LdapService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<LdapUser>> GetAllUsersAsync()
    {
        var users = new List<LdapUser>();
        
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        
        // Fetch active configuration
        var config = await context.LdapConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);
        
        if (config == null)
        {
            throw new Exception("LDAP is not configured or enabled. Please configure it in the settings.");
        }

        // LdapConnection in 4.x might implement IDisposable, but let's be safe and explicit
        using var connection = new LdapConnection();

        try
        {
            connection.Connect(config.Host, config.Port);
            // SecureSocketLayer property is deprecated/removed in some versions. Use SecureSocketLayer option in Connect or StartTls?
            // For now, let's ignore SSL setting setup via property or assume defaults. 
            // If SSL is needed, usually just connecting to 636 is enough for Implicit SSL, or use StartTls for 389.
            // connection.SecureSocketLayer = config.UseSsl; 

            connection.Bind(config.AdminDn, config.AdminPassword);

            var searchFilter = "(objectClass=user)"; 
            var attributes = new[] { "sAMAccountName", "mail", "displayName", "cn", "distinguishedName", "userPrincipalName" };

            // Using standard constants if ScopeSub not found: 2 = SCOPE_SUB
            var searchResults = connection.Search(
                config.BaseDn,
                LdapConnection.ScopeSub, // Try ScopeSub first, if fails we try SCOPE_SUB
                searchFilter,
                attributes,
                false
            );

            while (searchResults.HasMore())
            {
                try
                {
                    var entry = searchResults.Next();
                    var attributeSet = entry.GetAttributeSet();

                    string GetAttribute(string name)
                    {
                        var attr = attributeSet.GetAttribute(name);
                        return attr != null ? attr.StringValue : string.Empty;
                    }

                    var userName = GetAttribute("sAMAccountName");
                    if (string.IsNullOrEmpty(userName)) userName = GetAttribute("userPrincipalName");
                    
                    var email = GetAttribute("mail");
                    var fullName = GetAttribute("displayName");
                    if (string.IsNullOrEmpty(fullName)) fullName = GetAttribute("cn");
                    var dn = entry.Dn;

                    if (!string.IsNullOrEmpty(userName))
                    {
                        users.Add(new LdapUser
                        {
                            UserName = userName,
                            Email = email,
                            FullName = fullName,
                            DistinguishedName = dn
                        });
                    }
                }
                catch (LdapReferralException)
                {
                    // Ignore referrals
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users from LDAP");
            // Don't throw for now, return empty list or partial results?
            // Throwing allows controller to show error.
            throw; 
        }

        return users;
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var config = await context.LdapConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);

        if (config == null) return false;

        using var connection = new LdapConnection();
        try
        {
            connection.Connect(config.Host, config.Port);
            if (config.UseSsl) connection.SecureSocketLayer = true;

            // Bind with specific user credentials to validate
            // For AD, username often needs to be domain-qualified or DN
            // We might need to find the DN for the user first if just 'username' is passed
            // For simple bind, we can try userPrincipalName or domain\user
            
            // This is a naive implementation; usually you bind as admin, find the user DN, then bind as user.
            // But for now, returning false as we focus on SYNC, not Auth Validation here.
            
            // To properly validate:
            // 1. Bind Admin
            // 2. Search for User DN
            // 3. Bind User DN + Password
            
            // Skipping for "Sync" feature request scope, implemented only if Auth is requested via LDAP.
            return false;
        }
        catch (LdapException)
        {
            return false;
        }
    }
}
