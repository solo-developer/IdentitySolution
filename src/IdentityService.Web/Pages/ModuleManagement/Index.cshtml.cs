using IdentityService.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityService.Web.Pages.ModuleManagement;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly IConsulService _consulService;

    public IndexModel(IConsulService consulService)
    {
        _consulService = consulService;
    }

    public List<ServiceInfo> Services { get; set; } = new();
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }

    public async Task OnGetAsync()
    {
        Services = await _consulService.GetRegisteredServicesAsync();
        
        // Calculate health statistics
        HealthyCount = Services.Count(s => s.Status.Equals("passing", StringComparison.OrdinalIgnoreCase));
        WarningCount = Services.Count(s => s.Status.Equals("warning", StringComparison.OrdinalIgnoreCase));
        CriticalCount = Services.Count(s => s.Status.Equals("critical", StringComparison.OrdinalIgnoreCase));
    }

    public string GetStatusBadgeClass(string status)
    {
        return status.ToLower() switch
        {
            "passing" => "bg-success",
            "warning" => "bg-warning",
            "critical" => "bg-danger",
            _ => "bg-secondary"
        };
    }
}
