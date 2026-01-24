using IdentityService.Web.ViewModels;
using IdentityService.Domain.Entities;
using MassTransit;
using IdentitySolution.Shared.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace IdentityService.Web.Pages.UserManagement.Users;

[Authorize(Roles = "Administrator")]
public class CreateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateModel(UserManager<ApplicationUser> userManager, IPublishEndpoint publishEndpoint)
    {
        _userManager = userManager;
        _publishEndpoint = publishEndpoint;
    }

    [BindProperty]
    public CreateUserRequest Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = new ApplicationUser
        {
            UserName = Input.UserName,
            Email = Input.Email,
            FullName = Input.FullName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
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

            return RedirectToPage("Index");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
