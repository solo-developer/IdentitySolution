using IdentityService.Application.Interfaces;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using IdentitySolution.Shared.Events;

namespace IdentityService.Api.Controllers;

[Authorize(Roles = Roles.Administrator)]
[ApiController]
[Route("api/[controller]")]
public class UserManagementController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public UserManagementController(
        UserManager<ApplicationUser> userManager, 
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context,
        IPublishEndpoint publishEndpoint)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        return Ok(users);
    }

    [HttpPost("users/{userId}/roles")]
    public async Task<IActionResult> AssignRoleToUser(string userId, [FromBody] string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("User not found");

        var result = await _userManager.AddToRoleAsync(user, roleName);
        
        if (result.Succeeded)
        {
            await _publishEndpoint.Publish<IUserUpdated>(new
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName
            });
        }

        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleManager.Roles.ToListAsync();
        return Ok(roles);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var result = await _roleManager.CreateAsync(new ApplicationRole 
        { 
            Name = request.Name, 
            Description = request.Description 
        });
        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _context.Permissions.ToListAsync();
        return Ok(permissions);
    }

    [HttpPost("roles/{roleId}/permissions")]
    public async Task<IActionResult> AssignPermissionToRole(string roleId, [FromBody] Guid permissionId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null) return NotFound("Role not found");

        var permission = await _context.Permissions.FindAsync(permissionId);
        if (permission == null) return NotFound("Permission not found");

        if (await _context.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId))
            return BadRequest("Permission already assigned to role");

        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        await _context.SaveChangesAsync();

        return Ok();
    }
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
