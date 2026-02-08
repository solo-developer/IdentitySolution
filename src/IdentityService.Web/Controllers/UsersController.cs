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
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IApplicationDbContext _context;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IPublishEndpoint publishEndpoint,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _publishEndpoint = publishEndpoint;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        var model = new UserIndexViewModel();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Users.Add(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError("Email", "Email already exists.");
            return View(model);
        }

        if (await _userManager.FindByNameAsync(model.UserName) != null)
        {
            ModelState.AddModelError("UserName", "Username already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _publishEndpoint.Publish<IUserCreated>(new
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FullName = user.FullName,
                IsActive = user.IsActive
            });

            return RedirectToAction("Index");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        
        // Fetch all roles to group
        var allRolesEntities = await _roleManager.Roles.ToListAsync();
        
        var groupedRoles = allRolesEntities
            .GroupBy(r => r.Module ?? "Default")
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new RoleDto 
                { 
                    Id = r.Id, 
                    Name = r.Name ?? "", 
                    Description = r.Description ?? "",
                    Module = r.Module ?? ""
                }).ToList()
            );

        var model = new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            IsActive = user.IsActive,
            SelectedRoles = roles.ToList(),
            GroupedRoles = groupedRoles
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        bool hasChanges = false;
        if (user.IsActive != model.IsActive)
        {
            user.IsActive = model.IsActive;
            await _userManager.UpdateAsync(user);
            hasChanges = true;
        }

        // Update Roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var toAdd = model.SelectedRoles.Except(currentRoles).ToList();
        var toRemove = currentRoles.Except(model.SelectedRoles).ToList();

        if (toAdd.Any()) await _userManager.AddToRolesAsync(user, toAdd);
        if (toRemove.Any()) await _userManager.RemoveFromRolesAsync(user, toRemove);

        if (hasChanges || toAdd.Any() || toRemove.Any())
        {
            var finalRoles = await _userManager.GetRolesAsync(user);

            await _publishEndpoint.Publish<IUserUpdated>(new 
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Roles = finalRoles.ToList()
            });
        }

        TempData["SuccessMessage"] = "User updated successfully";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> ModuleRestrictions(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Get all active modules from the database
        var allModules = await _context.Modules
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();
            
        var restrictedModuleIds = await _context.UserModuleRestrictions
            .Where(r => r.UserId == id)
            .Select(r => r.ModuleId)
            .ToListAsync();

        var model = new UserModuleRestrictionsViewModel
        {
            UserId = user.Id,
            UserName = user.UserName ?? "",
            FullName = user.FullName ?? "",
            Modules = allModules.Select(m => new ModuleRestrictionItem
            {
                ModuleId = m.Id,
                ModuleName = m.Name,
                IsRestricted = restrictedModuleIds.Contains(m.Id)
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ModuleRestrictions(UserModuleRestrictionsViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null) return NotFound();

        // Get current restrictions
        var currentRestrictions = await _context.UserModuleRestrictions
            .Where(r => r.UserId == model.UserId)
            .ToListAsync();

        // Find what to add and remove
        var newRestrictedModuleIds = model.Modules
            .Where(m => m.IsRestricted)
            .Select(m => m.ModuleId)
            .ToList();

        var currentRestrictedModuleIds = currentRestrictions
            .Select(r => r.ModuleId)
            .ToList();

        // Remove restrictions that are no longer selected
        var toRemove = currentRestrictions
            .Where(r => !newRestrictedModuleIds.Contains(r.ModuleId))
            .ToList();
        
        // Add new restrictions
        var toAdd = newRestrictedModuleIds
            .Where(id => !currentRestrictedModuleIds.Contains(id))
            .Select(id => new UserModuleRestriction
            {
                UserId = model.UserId,
                ModuleId = id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name
            })
            .ToList();

        if (toRemove.Any())
        {
            _context.UserModuleRestrictions.RemoveRange(toRemove);
        }

        if (toAdd.Any())
        {
            await _context.UserModuleRestrictions.AddRangeAsync(toAdd);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Module restrictions updated successfully";
        return RedirectToAction("Index");
    }
}


