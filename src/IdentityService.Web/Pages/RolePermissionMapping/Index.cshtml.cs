using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.ViewModels;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IdentitySolution.Shared.Events;

namespace IdentityService.Web.Pages.RolePermissionMapping;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public IndexModel(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _roleManager = roleManager;
        _publishEndpoint = publishEndpoint;
    }

    public List<string> AvailableModules { get; set; } = new();
    public string? SelectedModule { get; set; }
    public List<RoleDto> RolesInModule { get; set; } = new();
    public string? SelectedRoleId { get; set; }
    public string? SelectedRoleName { get; set; }
    public Dictionary<string, List<PermissionTreeNode>> PermissionTree { get; set; } = new();

    public async Task OnGetAsync(string? module, string? roleId)
    {
        // Get all available modules from roles
        AvailableModules = await _roleManager.Roles
            .Select(r => r.Module ?? "Default")
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        if (string.IsNullOrEmpty(module))
        {
            return;
        }

        SelectedModule = module;

        // Get roles in selected module
        RolesInModule = await _roleManager.Roles
            .Where(r => r.Module == module)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name ?? "",
                Description = r.Description ?? "",
                Module = r.Module ?? ""
            })
            .ToListAsync();

        if (string.IsNullOrEmpty(roleId))
        {
            return;
        }

        SelectedRoleId = roleId;

        // Get role details
        var role = await _roleManager.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return;
        }

        SelectedRoleName = role.Name;

        // Get all active permissions for this module
        var permissions = await _context.Permissions
            .Where(p => p.Module == module && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Get assigned permission IDs
        var assignedPermissionIds = role.RolePermissions
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        // Build permission tree grouped by category (first part of permission name before '.')
        PermissionTree = permissions
            .GroupBy(p => GetPermissionCategory(p.Name))
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => new PermissionTreeNode
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Module = p.Module,
                    IsAssigned = assignedPermissionIds.Contains(p.Id)
                }).ToList()
            );
    }

    public async Task<IActionResult> OnPostAsync(string roleId, string module, List<Guid> selectedPermissions)
    {
        var role = await _roleManager.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return NotFound();
        }

        // Remove all existing role permissions
        _context.RolePermissions.RemoveRange(role.RolePermissions);

        // Add new role permissions
        foreach (var permissionId in selectedPermissions)
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }

        await _context.SaveChangesAsync();

        // Get permission names for event
        var permissionNames = await _context.Permissions
            .Where(p => selectedPermissions.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync();

        // Publish role updated event
        await _publishEndpoint.Publish<IRoleUpdated>(new
        {
            RoleId = role.Id,
            Name = role.Name,
            Module = role.Module,
            Permissions = permissionNames
        });

        TempData["SuccessMessage"] = "Role permissions updated successfully";
        return RedirectToPage(new { module, roleId });
    }

    private string GetPermissionCategory(string permissionName)
    {
        // Extract category from permission name (e.g., "User.Create" -> "User")
        var parts = permissionName.Split('.');
        return parts.Length > 1 ? parts[0] : "General";
    }
}
