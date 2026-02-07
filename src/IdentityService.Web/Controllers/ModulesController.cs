using IdentityService.Web.Services;
using IdentityService.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class ModulesController : Controller
{
    private readonly IConsulService _consulService;
    private readonly IdentityService.Application.Interfaces.IApplicationDbContext _context;

    public ModulesController(IConsulService consulService, IdentityService.Application.Interfaces.IApplicationDbContext context)
    {
        _consulService = consulService;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var model = new ModulesViewModel();
        var consulServices = await _consulService.GetRegisteredServicesAsync();
        
        // 1. Get all modules from database
        var dbModules = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_context.Modules);
        
        // 2. Identify modules that are in DB but NOT active in Consul
        // We'll group consul services by their 'Module' tag/property
        var activeModuleNames = consulServices.Select(s => s.Module).Distinct().ToList();
        
        var offlineModules = dbModules
            .Where(m => !activeModuleNames.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // 3. Add active services to model
        model.Services.AddRange(consulServices);

        // 4. Add offline modules as placeholder services
        foreach (var module in offlineModules)
        {
            model.Services.Add(new ServiceInfo
            {
                Id = module.Id.ToString(),
                Name = module.Name,
                Module = module.Name,
                Status = "critical", // Show as critical since it's registered but down
                Address = "N/A",
                Port = 0,
                Tags = new List<string> { "Offline", "Registered" }
            });
        }

        // Calculate health statistics
        model.HealthyCount = model.Services.Count(s => s.Status.Equals("passing", StringComparison.OrdinalIgnoreCase));
        model.WarningCount = model.Services.Count(s => s.Status.Equals("warning", StringComparison.OrdinalIgnoreCase));
        model.CriticalCount = model.Services.Count(s => s.Status.Equals("critical", StringComparison.OrdinalIgnoreCase));

        return View(model);
    }
}
