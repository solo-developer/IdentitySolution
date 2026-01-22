using System;
using System.Collections.Generic;

namespace IdentityService.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty; // e.g., "UserManagement", "RoleManagement"
    
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
