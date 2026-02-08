namespace IdentityService.Domain.Entities;

/// <summary>
/// Represents a module that a user is restricted from accessing.
/// When a user has a restriction for a module, they will be denied SSO access to that module.
/// </summary>
public class UserModuleRestriction
{
    public int Id { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
