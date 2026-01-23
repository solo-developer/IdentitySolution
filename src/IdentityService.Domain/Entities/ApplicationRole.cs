using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities;

public class ApplicationRole : IdentityRole
{
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
