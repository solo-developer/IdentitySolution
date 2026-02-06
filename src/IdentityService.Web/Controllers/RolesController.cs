using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Web.ViewModels;
using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class RolesController : Controller
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IdentityService.Web.Services.IConsulService _consulService;

    public RolesController(
        RoleManager<ApplicationRole> roleManager, 
        IApplicationDbContext context, 
        IPublishEndpoint publishEndpoint,
        IdentityService.Web.Services.IConsulService consulService)
    {
        _roleManager = roleManager;
        _context = context;
        _publishEndpoint = publishEndpoint;
        _consulService = consulService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? module)
    {
        var model = new RoleIndexViewModel();
        model.Module = module;

        // Get Distinct Modules from Consul + DB
        model.Modules = await _consulService.GetAllModulesAsync();

        if (!string.IsNullOrEmpty(model.Module))
        {
            // Lock module if passed via query
            model.IsModuleReadOnly = true; 

            var roles = await _roleManager.Roles
                .Where(r => r.Module == model.Module)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync();

            model.Roles = roles.Select(r => new RoleDto
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
            model.CreateRoleInput.Module = model.Module;
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(RoleIndexViewModel model)
    {
        // Bind CreateRoleInput is part of model.
        
        // We need to re-populate lists if validation fails, similar to Index
        
        if (!ModelState.IsValid) 
        {
             // Re-load data
             return await Index(model.CreateRoleInput.Module ?? model.Module);
        }

        var input = model.CreateRoleInput;

        if (string.IsNullOrEmpty(input.Module)) 
        {
            ModelState.AddModelError("CreateRoleInput.Module", "Module is required.");
            // We need modules loaded for the view to render correctly even on error
            // Calling Index logic usually helps, but we need to merge errors.
            // Returning the View(model) directly requires manually repopulating Modules too.
            // Calling Index(module) will create NEW model, losing the errors? No, ModelState persists if we return View.
            // But if we call Index(), it returns View(new model).
            // So we must manually repopulate.
            return await ReloadAndReturnView(model);
        }

        if (await _roleManager.RoleExistsAsync(input.Name))
        {
             ModelState.AddModelError("CreateRoleInput.Name", "Role already exists.");
             return await ReloadAndReturnView(model);
        }

        // Validate Module and Get ID
        var moduleEntity = await _context.Modules.FirstOrDefaultAsync(m => m.Name == input.Module && m.IsActive);
        
        if (moduleEntity == null)
        {
             ModelState.AddModelError("CreateRoleInput.Module", $"Module '{input.Module}' is not a valid registered module.");
             return await ReloadAndReturnView(model);
        }

        var role = new ApplicationRole
        {
            Name = input.Name,
            Description = input.Description,
            Module = input.Module,
            ModuleId = moduleEntity.Id
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

            // Redirect to get fresh state
            return RedirectToAction("Index", new { module = input.Module });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        
        return await ReloadAndReturnView(model);
    }

    private async Task<IActionResult> ReloadAndReturnView(RoleIndexViewModel model)
    {
        model.Modules = await _consulService.GetAllModulesAsync();
        model.Module = model.CreateRoleInput.Module ?? model.Module;
        
        if (!string.IsNullOrEmpty(model.Module))
        {
             var roles = await _roleManager.Roles
                .Where(r => r.Module == model.Module)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync();

            model.Roles = roles.Select(r => new RoleDto
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
        }
        return View("Index", model); // Explicitly use Index view
    }
}
