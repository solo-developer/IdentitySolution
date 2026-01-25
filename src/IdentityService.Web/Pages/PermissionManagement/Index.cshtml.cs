using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.ViewModels;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IdentitySolution.Shared.Events;

namespace IdentityService.Web.Pages.PermissionManagement;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly Consul.IConsulClient _consulClient;

    public IndexModel(ApplicationDbContext context, IPublishEndpoint publishEndpoint, Consul.IConsulClient consulClient)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _consulClient = consulClient;
    }

    public List<PermissionViewModel> Permissions { get; set; } = new();
    public List<string> AvailableModules { get; set; } = new();
    public string? SelectedModule { get; set; }
    public string? StatusFilter { get; set; }

    public async Task OnGetAsync(string? module, string? status)
    {
        SelectedModule = module;
        StatusFilter = status;

        // Get all available modules
        // Get available modules from Consul + DB
        var services = await _consulClient.Catalog.Services();
        var consulModules = services.Response.Keys
            .Where(k => !string.Equals(k, "consul", StringComparison.OrdinalIgnoreCase) && 
                        !string.Equals(k, "IdentityService", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dbModules = await _context.Permissions
            .Select(p => p.Module)
            .Distinct()
            .ToListAsync();

        AvailableModules = consulModules.Union(dbModules)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        // Query permissions
        var query = _context.Permissions.AsQueryable();

        if (!string.IsNullOrEmpty(module))
        {
            query = query.Where(p => p.Module == module);
        }

        if (status == "active")
        {
            query = query.Where(p => p.IsActive);
        }
        else if (status == "inactive")
        {
            query = query.Where(p => !p.IsActive);
        }

        Permissions = await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Module = p.Module,
                IsActive = p.IsActive
            })
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(CreatePermissionRequest request)
    {
        if (!ModelState.IsValid) return Page();

        // Validate Module Exists
        var services = await _consulClient.Catalog.Services();
        var knownModules = services.Response.Keys.ToList();
        var dbModules = await _context.Permissions.Select(p => p.Module).Distinct().ToListAsync();
        
        if (!knownModules.Contains(request.Module, StringComparer.OrdinalIgnoreCase) && 
            !dbModules.Contains(request.Module, StringComparer.OrdinalIgnoreCase))
        {
             ModelState.AddModelError("Module", $"Module '{request.Module}' is not a valid registered module.");
             // Refresh available modules for re-redisplay
             AvailableModules = knownModules.Union(dbModules).Distinct().OrderBy(m => m).ToList();
             return Page();
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Module = request.Module,
            IsActive = true
        };

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();

        // Publish permission created event
        await _publishEndpoint.Publish<IPermissionCreated>(new
        {
            PermissionId = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Module = permission.Module,
            IsActive = permission.IsActive
        });

        TempData["SuccessMessage"] = "Permission created successfully";
        return RedirectToPage(new { module = request.Module });
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid permissionId, UpdatePermissionRequest request)
    {
        var permission = await _context.Permissions.FindAsync(permissionId);
        if (permission == null)
        {
            return NotFound();
        }

        permission.Name = request.Name;
        permission.Description = request.Description;
        permission.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        // Publish permission updated event
        await _publishEndpoint.Publish<IPermissionUpdated>(new
        {
            PermissionId = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Module = permission.Module,
            IsActive = permission.IsActive
        });

        TempData["SuccessMessage"] = "Permission updated successfully";
        return RedirectToPage(new { module = permission.Module });
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(Guid permissionId)
    {
        var permission = await _context.Permissions.FindAsync(permissionId);
        if (permission == null)
        {
            return NotFound();
        }

        permission.IsActive = !permission.IsActive;
        await _context.SaveChangesAsync();

        // Publish permission updated event
        await _publishEndpoint.Publish<IPermissionUpdated>(new
        {
            PermissionId = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Module = permission.Module,
            IsActive = permission.IsActive
        });

        TempData["SuccessMessage"] = $"Permission {(permission.IsActive ? "enabled" : "disabled")} successfully";
        return RedirectToPage(new { module = permission.Module });
    }
}
