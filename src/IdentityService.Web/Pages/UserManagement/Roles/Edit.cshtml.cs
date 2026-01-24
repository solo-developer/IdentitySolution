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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Web.Pages.UserManagement.Roles;

[Authorize(Roles = "Administrator")]
public class EditModel : PageModel
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public EditModel(RoleManager<ApplicationRole> roleManager, IApplicationDbContext context, IPublishEndpoint publishEndpoint)
    {
        _roleManager = roleManager;
        _context = context;
        _publishEndpoint = publishEndpoint;
    }

    public RoleDto Role { get; set; }
    public List<PermissionDto> AllPermissions { get; set; } = new();

    [BindProperty]
    public List<Guid> SelectedPermissionIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var r = await _roleManager.Roles
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null) return NotFound();

        Role = new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Module = r.Module
        };

        // Get permissions ONLY for this module
        var perms = await _context.Permissions
            .Where(p => p.Module == r.Module)
            .OrderBy(p => p.Name)
            .ToListAsync();

        AllPermissions = perms.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Module = p.Module,
            IsAssigned = r.RolePermissions.Any(rp => rp.PermissionId == p.Id)
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        // Clear existing permissions
        var existing = await _context.RolePermissions
            .Where(rp => rp.RoleId == id)
            .ToListAsync();
        _context.RolePermissions.RemoveRange(existing);

        // Add new
        if (SelectedPermissionIds.Any())
        {
            var newPerms = SelectedPermissionIds.Select(pid => new RolePermission
            {
                RoleId = id,
                PermissionId = pid
            });
            await _context.RolePermissions.AddRangeAsync(newPerms);
        }

        await _context.SaveChangesAsync();

        // Get updated permission names for event
        var permissionNames = await _context.Permissions
            .Where(p => SelectedPermissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync();

        await _publishEndpoint.Publish<IRoleUpdated>(new 
        {
            RoleId = role.Id,
            Name = role.Name,
            Module = role.Module,
            Permissions = permissionNames
        });

        return RedirectToPage("Index", new { module = role.Module });
    }
}
