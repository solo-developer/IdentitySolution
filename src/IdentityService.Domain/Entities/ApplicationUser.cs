using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // LDAP Integration
    public bool IsLdapUser { get; set; }
    public string? LdapDistinguishedName { get; set; }
    
    // Module Restrictions - modules this user cannot access
    public ICollection<UserModuleRestriction> ModuleRestrictions { get; set; } = new List<UserModuleRestriction>();
}

