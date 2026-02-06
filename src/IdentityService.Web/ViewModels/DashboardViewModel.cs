namespace IdentityService.Web.ViewModels;

public class DashboardViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalPermissions { get; set; }
    public int TotalModules { get; set; }
    public Dictionary<string, int> RolesByModule { get; set; } = new();
    public string Environment { get; set; } = string.Empty;
}
