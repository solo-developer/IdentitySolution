using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityService.Web.ViewModels;

public class PermissionViewModel
{
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Module { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public List<PermissionViewModel> Children { get; set; } = new();
}

public class CreatePermissionRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Module { get; set; } = string.Empty;
    
    public Guid? ParentId { get; set; }
}

public class UpdatePermissionRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public bool IsActive { get; set; }
    
    public Guid? ParentId { get; set; }
}

public class PermissionTreeNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
    public string Module { get; set; } = string.Empty;
    public List<PermissionTreeNode> Children { get; set; } = new();
}
