using IdentityService.Web.ViewModels;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using MassTransit;
using IdentitySolution.Shared.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Web.Pages.UserManagement.Roles;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public IndexModel(RoleManager<ApplicationRole> roleManager, IApplicationDbContext context, IPublishEndpoint publishEndpoint)
    {
        _roleManager = roleManager;
        _context = context;
        _publishEndpoint = publishEndpoint;
    }

    [BindProperty(SupportsGet = true)]
    public string? Module { get; set; }

    public List<string> Modules { get; set; } = new();
    public List<RoleDto> Roles { get; set; } = new();
    
    public bool IsModuleReadOnly { get; set; }

    [BindProperty]
    public CreateRoleRequest CreateRoleInput { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Get Distinct Modules
        Modules = await _context.Permissions
            .Select(p => p.Module)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        if (!string.IsNullOrEmpty(Module))
        {
            // Lock module if passed via query (simplified interpretation of "read only")
            // In a real scenario, we might want to check if the user *really* came from that service, 
            // but for now, if the link specifies it, we lock it for UX.
            IsModuleReadOnly = true; 

            var roles = await _roleManager.Roles
                .Where(r => r.Module == Module)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync();

            Roles = roles.Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Module = r.Module,
                Permissions = r.RolePermissions.Select(rp => new PermissionDto
                {
                    Id = rp.Permission.Id,
                    Name = rp.Permission.Name,
                    Description = rp.Permission.Description,
                    Module = rp.Permission.Module,
                    IsAssigned = true
                }).ToList()
            }).ToList();
            
            // Pre-fill create input module
            CreateRoleInput.Module = Module;
        }
    }
    
    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (string.IsNullOrEmpty(CreateRoleInput.Module)) 
        {
            ModelState.AddModelError("", "Module is required.");
            // Re-load data
            await OnGetAsync();
            return Page();
        }

        if (await _roleManager.RoleExistsAsync(CreateRoleInput.Name))
        {
             ModelState.AddModelError("", "Role already exists.");
             await OnGetAsync();
             return Page();
        }

        var role = new ApplicationRole
        {
            Name = CreateRoleInput.Name,
            Description = CreateRoleInput.Description,
            Module = CreateRoleInput.Module
        };

        var result = await _roleManager.CreateAsync(role);
        if (result.Succeeded)
        {
            await _publishEndpoint.Publish<IRoleCreated>(new 
            {
                RoleId = role.Id,
                Name = role.Name,
                Description = role.Description,
                Module = role.Module
            });

            return RedirectToPage(new { module = CreateRoleInput.Module });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        
        await OnGetAsync();
        return Page();
    }
}
