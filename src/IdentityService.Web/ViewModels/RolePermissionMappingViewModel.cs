using IdentityService.Web.ViewModels;

namespace IdentityService.Web.ViewModels;

public class RolePermissionMappingViewModel
{
    public List<string> AvailableModules { get; set; } = new();
    public string? SelectedModule { get; set; }
    public List<RoleDto> RolesInModule { get; set; } = new();
    public string? SelectedRoleId { get; set; }
    public string? SelectedRoleName { get; set; }
    public Dictionary<string, List<PermissionTreeNode>> PermissionTree { get; set; } = new();
}
