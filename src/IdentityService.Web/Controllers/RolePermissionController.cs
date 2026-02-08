using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.ViewModels;
using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class RolePermissionController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public RolePermissionController(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _roleManager = roleManager;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? module, string? roleId)
    {
        var model = new RolePermissionMappingViewModel();

        // Get available modules from persistent Modules table
        model.AvailableModules = await _context.Modules
            .Where(m => m.IsActive)
            .Select(m => m.Name)
            .OrderBy(m => m)
            .ToListAsync();

        if (string.IsNullOrEmpty(module))
        {
            return View(model);
        }

        model.SelectedModule = module;

        // Get roles in selected module
        model.RolesInModule = await _roleManager.Roles
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
            return View(model);
        }

        model.SelectedRoleId = roleId;

        // Get role details
        var role = await _roleManager.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return View(model);
        }

        model.SelectedRoleName = role.Name;

        // Get all active permissions for this module
        var permissions = await _context.Permissions
            .Where(p => p.Module == module && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Get assigned permission IDs
        var assignedPermissionIds = role.RolePermissions
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        // Build permission tree based on ParentId
        model.PermissionTree = BuildPermissionTree(permissions, assignedPermissionIds);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Assign(string roleId, string module, List<Guid> selectedPermissions)
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
        return RedirectToAction("Index", new { module, roleId });
    }

    private List<PermissionTreeNode> BuildPermissionTree(List<Permission> permissions, HashSet<Guid> assignedPermissionIds, Guid? parentId = null)
    {
        return permissions
            .Where(p => p.ParentId == parentId)
            .Select(p => new PermissionTreeNode
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Module = p.Module,
                IsAssigned = assignedPermissionIds.Contains(p.Id),
                Children = BuildPermissionTree(permissions, assignedPermissionIds, p.Id)
            })
            .ToList();
    }
}
