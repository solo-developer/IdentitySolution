using IdentityService.Web.ViewModels;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;
using IdentitySolution.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Web.Controllers;

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

    #region User Management

    [HttpGet("users")]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }

        return Ok(userDtos);
    }

    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            Roles = roles.ToList()
        });
    }

    [HttpPut("users/{id}/roles")]
    public async Task<IActionResult> UpdateUserRoles(string id, [FromBody] AssignUserRolesRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound("User not found");

        var currentRoles = await _userManager.GetRolesAsync(user);
        
        var rolesToAdd = request.RoleNames.Except(currentRoles).ToList();
        var rolesToRemove = currentRoles.Except(request.RoleNames).ToList();

        if (rolesToAdd.Any())
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded) return BadRequest(addResult.Errors);
        }

        if (rolesToRemove.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded) return BadRequest(removeResult.Errors);
        }

        if (rolesToAdd.Any() || rolesToRemove.Any())
        {
            await _publishEndpoint.Publish<IUserUpdated>(new
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName
            });
        }

        return Ok();
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> ToggleUserStatus(string id, [FromBody] bool isActive)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound("User not found");

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            await _publishEndpoint.Publish<IUserUpdated>(new
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName
            });
            return Ok();
        }

        return BadRequest(result.Errors);
    }

    #endregion

    #region Module Management

    [HttpGet("modules")]
    public async Task<ActionResult<List<string>>> GetModules()
    {
        // Get distinct modules from Permissions. 
        // We could also get from Roles, but Permissions is the source of truth for "Registered Modules".
        var modules = await _context.Permissions
            .Select(p => p.Module)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        return Ok(modules);
    }

    #endregion

    #region Role Management

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles([FromQuery] string? module = null)
    {
        var query = _roleManager.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!string.IsNullOrEmpty(module))
        {
            query = query.Where(r => r.Module == module);
        }

        var roles = await query.ToListAsync();

        var roleDtos = roles.Select(r => new RoleDto
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

        return Ok(roleDtos);
    }

    [HttpGet("roles/{id}")]
    public async Task<ActionResult<RoleDto>> GetRole(string id)
    {
        var role = await _roleManager.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();

        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            Module = role.Module,
            Permissions = role.RolePermissions.Select(rp => new PermissionDto
            {
                Id = rp.Permission.Id,
                Name = rp.Permission.Name,
                Description = rp.Permission.Description,
                Module = rp.Permission.Module,
                IsAssigned = true
            }).ToList()
        });
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (await _roleManager.RoleExistsAsync(request.Name))
            return BadRequest("Role already exists");

        var role = new ApplicationRole
        {
            Name = request.Name,
            Description = request.Description,
            Module = request.Module // Assign module from request
        };

        var result = await _roleManager.CreateAsync(role);
        return result.Succeeded ? Ok(new { id = role.Id }) : BadRequest(result.Errors);
    }

    [HttpPut("roles/{id}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(string id, [FromBody] UpdateRolePermissionsRequest request)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound("Role not found");

        // Clear existing permissions
        var existingPermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == id)
            .ToListAsync();
        
        _context.RolePermissions.RemoveRange(existingPermissions);

        // Add new permissions
        if (request.PermissionIds != null && request.PermissionIds.Any())
        {
            // Optional: Validate that permissions belong to the same module as the role? 
            // The requirement "module role wise permissions" hints at this, but flexibility might be better.
            
            var newPermissions = request.PermissionIds.Select(pid => new RolePermission
            {
                RoleId = id,
                PermissionId = pid
            });
            await _context.RolePermissions.AddRangeAsync(newPermissions);
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    #endregion

    #region Permission Management

    [HttpGet("permissions")]
    public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions([FromQuery] string? module = null)
    {
        var query = _context.Permissions.AsQueryable();

        if (!string.IsNullOrEmpty(module))
        {
            query = query.Where(p => p.Module == module);
        }

        var permissions = await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return Ok(permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Module = p.Module,
            IsAssigned = false
        }));
    }

    #endregion
}
