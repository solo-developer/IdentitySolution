using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Web.Pages;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(
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

    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalPermissions { get; set; }
    public int TotalModules { get; set; }
    public Dictionary<string, int> RolesByModule { get; set; } = new();
    public string Environment { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        // Get user statistics
        var users = await _userManager.Users.ToListAsync();
        TotalUsers = users.Count;
        ActiveUsers = users.Count(u => u.IsActive);
        InactiveUsers = users.Count(u => !u.IsActive);

        // Get role statistics
        var roles = await _roleManager.Roles.ToListAsync();
        TotalRoles = roles.Count;
        
        // Group roles by module
        RolesByModule = roles
            .GroupBy(r => r.Module ?? "Default")
            .ToDictionary(g => g.Key, g => g.Count());
        
        TotalModules = RolesByModule.Count;

        // Get permission statistics
        TotalPermissions = await _context.Permissions.CountAsync();

        // Environment info
        Environment = _environment.EnvironmentName;
    }
}
