using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentitySolution.Shared.Events;
using IdentitySolution.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure.Consumers;

public class RegisterModuleConsumer : IConsumer<IRegisterModule>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<RegisterModuleConsumer> _logger;

    public RegisterModuleConsumer(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context,
        ILogger<RegisterModuleConsumer> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IRegisterModule> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing registration for module: {ModuleName}", message.ModuleName);

        // 1. Process Permissions
        foreach (var pReq in message.Permissions)
        {
            if (!await _context.Permissions.AnyAsync(p => p.Name == pReq.Name))
            {
                _context.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = pReq.Name,
                    Module = pReq.Module,
                    Description = pReq.Description
                });
            }
        }
        await _context.SaveChangesAsync();

        // 2. Process Roles
        foreach (var rReq in message.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(rReq.Name))
            {
                await _roleManager.CreateAsync(new ApplicationRole
                {
                    Name = rReq.Name,
                    Description = rReq.Description
                });
            }
        }

        // 3. Process Users (Idempotent insertion)
        foreach (var uReq in message.Users)
        {
            var user = await _userManager.FindByNameAsync(uReq.UserName);
            if (user == null)
            {
                _logger.LogInformation("Seeding new user: {UserName} for module {ModuleName}", uReq.UserName, message.ModuleName);
                var newUser = new ApplicationUser
                {
                    UserName = uReq.UserName,
                    Email = uReq.Email,
                    FullName = uReq.FullName,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(newUser, "DefaultPassword123!");
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to create user {UserName}: {Errors}", uReq.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogInformation("User {UserName} already exists. Skipping insertion.", uReq.UserName);
            }
        }
    }
}
