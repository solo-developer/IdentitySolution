using IdentityService.Web.ViewModels;

namespace IdentityService.Web.ViewModels;

public class RoleIndexViewModel
{
    public string? Module { get; set; }
    public List<string> Modules { get; set; } = new();
    public List<RoleDto> Roles { get; set; } = new();
    public bool IsModuleReadOnly { get; set; }
    public CreateRoleRequest CreateRoleInput { get; set; } = new();
}
