using IdentityService.Api.ViewModels;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Api.Pages.UserManagement.Users;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public List<UserDto> Users { get; set; } = new();

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        Users = new List<UserDto>();
        
        foreach(var u in users) 
        {
            var roles = await _userManager.GetRolesAsync(u);
            Users.Add(new UserDto {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                FullName = u.FullName,
                IsActive = u.IsActive,
                Roles = roles.ToList()
            });
        }
    }
}
