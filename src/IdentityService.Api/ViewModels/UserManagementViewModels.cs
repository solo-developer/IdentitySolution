
using System;
using System.Collections.Generic;

namespace IdentityService.Api.ViewModels;

public class UserDto
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class RoleDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Module { get; set; }
    public List<PermissionDto> Permissions { get; set; } = new();
}

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Module { get; set; }
    public bool IsAssigned { get; set; } // Helper for UI
}

public class AssignUserRolesRequest
{
    public List<string> RoleNames { get; set; } = new();
}

public class UpdateRolePermissionsRequest
{
    public List<Guid> PermissionIds { get; set; } = new();
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
}
