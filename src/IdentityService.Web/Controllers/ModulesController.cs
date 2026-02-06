using IdentityService.Web.Services;
using IdentityService.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class ModulesController : Controller
{
    private readonly IConsulService _consulService;

    public ModulesController(IConsulService consulService)
    {
        _consulService = consulService;
    }

    public async Task<IActionResult> Index()
    {
        var model = new ModulesViewModel();
        model.Services = await _consulService.GetRegisteredServicesAsync();
        
        // Calculate health statistics
        model.HealthyCount = model.Services.Count(s => s.Status.Equals("passing", StringComparison.OrdinalIgnoreCase));
        model.WarningCount = model.Services.Count(s => s.Status.Equals("warning", StringComparison.OrdinalIgnoreCase));
        model.CriticalCount = model.Services.Count(s => s.Status.Equals("critical", StringComparison.OrdinalIgnoreCase));

        return View(model);
    }
}
