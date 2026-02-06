using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.ViewModels;
using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class PermissionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly Consul.IConsulClient _consulClient;

    public PermissionsController(ApplicationDbContext context, IPublishEndpoint publishEndpoint, Consul.IConsulClient consulClient)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _consulClient = consulClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? module, string? status)
    {
        var model = new PermissionListViewModel();
        model.SelectedModule = module;
        model.StatusFilter = status;

        // Get available modules from DB
        model.AvailableModules = await _context.Modules
            .Where(m => m.IsActive)
            .Select(m => m.Name)
            .OrderBy(m => m)
            .ToListAsync();

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

        model.Permissions = await query
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

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PermissionListViewModel model)
    {
        // Note: The form binds to "name", "module" description directly, so model.CreatePermissionInput properties might not be populated if names don't match hierarchy.
        // However, standard MVC should bind if name is "CreatePermissionInput.Name" or if we accept CreatePermissionRequest directly.
        // But the original form used "name", "module".
        // I should likely accept `CreatePermissionRequest` as parameter or use manual binding.
        // For simplicity, I'll update the View to use `CreatePermissionInput.Name`.
        return await CreateInternal(model.CreatePermissionInput);
    }
    
    // Changing signature to match what I'll do in View (Use CreatePermissionInput)
    // Or simpler: Accept the properties.
    // Let's stick to the method accepting the Request object if I update the form.
    
    private async Task<IActionResult> CreateInternal(CreatePermissionRequest request)
    {
        if (!ModelState.IsValid) return await Index(request.Module, null);

        // Validate Module Exists
        var moduleEntity = await _context.Modules.FirstOrDefaultAsync(m => m.Name == request.Module && m.IsActive);
        
        if (moduleEntity == null)
        {
             ModelState.AddModelError("CreatePermissionInput.Module", $"Module '{request.Module}' is not a valid active module.");
             return await Index(request.Module, null);
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Module = request.Module,
            ModuleId = moduleEntity.Id,
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
        return RedirectToAction("Index", new { module = request.Module });
    }

    [HttpPost]
    public async Task<IActionResult> Update(Guid permissionId, UpdatePermissionRequest request)
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
        return RedirectToAction("Index", new { module = permission.Module });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(Guid permissionId)
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
        return RedirectToAction("Index", new { module = permission.Module });
    }
}
