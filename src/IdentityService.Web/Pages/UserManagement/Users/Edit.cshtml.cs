using IdentityService.Web.ViewModels;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using IdentitySolution.Shared.Events;

namespace IdentityService.Web.Pages.UserManagement.Users;

[Authorize(Roles = "Administrator")]
public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public EditModel(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IPublishEndpoint publishEndpoint)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _publishEndpoint = publishEndpoint;
    }

    public UserDto UserInfo { get; set; }
    public Dictionary<string, List<RoleDto>> GroupedRoles { get; set; } = new();

    [BindProperty]
    public List<string> SelectedRoles { get; set; } = new();

    [BindProperty]
    public bool IsActive { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var userRoles = await _userManager.GetRolesAsync(user);

        UserInfo = new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive
        };
        IsActive = user.IsActive;
        SelectedRoles = userRoles.ToList();

        // Load all roles and group by module
        var allRoles = await _roleManager.Roles.OrderBy(r => r.Module).ThenBy(r => r.Name).ToListAsync();
        
        GroupedRoles = allRoles
            .GroupBy(r => string.IsNullOrEmpty(r.Module) ? "Global" : r.Module)
            .ToDictionary(
                g => g.Key, 
                g => g.Select(r => new RoleDto { Name = r.Name, Description = r.Description }).ToList()
            );

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Update Status
        if (user.IsActive != IsActive)
        {
            user.IsActive = IsActive;
            await _userManager.UpdateAsync(user);
        }

        // Update Roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var toAdd = SelectedRoles.Except(currentRoles).ToList();
        var toRemove = currentRoles.Except(SelectedRoles).ToList();

        if (toAdd.Any()) await _userManager.AddToRolesAsync(user, toAdd);
        if (toRemove.Any()) await _userManager.RemoveFromRolesAsync(user, toRemove);

        // Get final roles for event
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

        return RedirectToPage("Index");
    }
}
