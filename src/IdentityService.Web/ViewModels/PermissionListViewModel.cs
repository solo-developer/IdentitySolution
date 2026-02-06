namespace IdentityService.Web.ViewModels;

public class PermissionListViewModel
{
    public List<PermissionViewModel> Permissions { get; set; } = new();
    public List<string> AvailableModules { get; set; } = new();
    public string? SelectedModule { get; set; }
    public string? StatusFilter { get; set; }
    
    // For Create Form
    public CreatePermissionRequest CreatePermissionInput { get; set; } = new();
}
