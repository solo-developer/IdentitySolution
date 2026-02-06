using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class HomeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public HomeController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel();

        // Get user statistics
        var users = await _userManager.Users.ToListAsync();
        model.TotalUsers = users.Count;
        model.ActiveUsers = users.Count(u => u.IsActive);
        model.InactiveUsers = users.Count(u => !u.IsActive);

        // Get role statistics
        var roles = await _roleManager.Roles.ToListAsync();
        model.TotalRoles = roles.Count;

        // Group roles by module
        model.RolesByModule = roles
            .GroupBy(r => r.Module ?? "Default")
            .ToDictionary(g => g.Key, g => g.Count());

        model.TotalModules = model.RolesByModule.Count;

        // Get permission statistics
        model.TotalPermissions = await _context.Permissions.CountAsync();

        // Environment info
        model.Environment = _environment.EnvironmentName;

        return View(model);
    }
}
