namespace IdentityService.Web.ViewModels;

public class UserIndexViewModel
{
    public List<UserDto> Users { get; set; } = new();
}

public class CreateUserViewModel : CreateUserRequest
{
    // Inherits properties
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    
    // Role Management
    public List<string> SelectedRoles { get; set; } = new();
    
    // For UI Display
    public List<string> AvailableRoles { get; set; } = new();
    
    // Grouped Roles for UI (Module -> Roles)
    public Dictionary<string, List<RoleDto>> GroupedRoles { get; set; } = new();
}
