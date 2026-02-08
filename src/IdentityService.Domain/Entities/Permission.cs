using System;
using System.Collections.Generic;

namespace IdentityService.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty; // e.g., "UserManagement", "RoleManagement"
    public bool IsActive { get; set; } = true;
    
    public Guid? ModuleId { get; set; }
    public virtual Module ModuleEntity { get; set; }
    
    public Guid? ParentId { get; set; }
    public virtual Permission? Parent { get; set; }
    public virtual ICollection<Permission> Children { get; set; } = new List<Permission>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
