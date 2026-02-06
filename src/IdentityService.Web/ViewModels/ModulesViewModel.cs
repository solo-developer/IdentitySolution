using IdentityService.Web.Services;

namespace IdentityService.Web.ViewModels;

public class ModulesViewModel
{
    public List<ServiceInfo> Services { get; set; } = new();
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
}
