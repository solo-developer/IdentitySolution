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
public class LdapController : Controller
{
    private readonly ILdapService _ldapService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LdapController> _logger;

    public LdapController(
        ILdapService ldapService,
        UserManager<ApplicationUser> userManager,
        IPublishEndpoint publishEndpoint,
        IApplicationDbContext context,
        ILogger<LdapController> logger)
    {
        _ldapService = ldapService;
        _userManager = userManager;
        _publishEndpoint = publishEndpoint;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Check if configuration exists
        var config = await _context.LdapConfigurations.FirstOrDefaultAsync(c => c.IsEnabled);
        if (config == null)
        {
            return RedirectToAction("Configure");
        }

        var model = new LdapSyncViewModel();
        try 
        {
            var ldapUsers = await _ldapService.GetAllUsersAsync();
            foreach (var ldapUser in ldapUsers)
            {
                var existingUser = await _userManager.FindByNameAsync(ldapUser.UserName);
                model.Users.Add(new LdapUserPreview
                {
                    UserName = ldapUser.UserName,
                    Email = ldapUser.Email,
                    FullName = ldapUser.FullName,
                    Status = existingUser == null ? "New" : "Existing"
                });
            }
        }
        catch (Exception ex)
        {
            model.Message = $"Error connecting to LDAP: {ex.Message}";
            _logger.LogError(ex, "LDAP connection failed");
        }
        
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Configure()
    {
        var config = await _context.LdapConfigurations.FirstOrDefaultAsync();
        var model = new LdapConfigurationViewModel();

        if (config != null)
        {
            model.Id = config.Id;
            model.Host = config.Host;
            model.Port = config.Port;
            model.BaseDn = config.BaseDn;
            model.AdminDn = config.AdminDn;
            model.AdminPassword = config.AdminPassword;
            model.UseSsl = config.UseSsl;
            model.IsEnabled = config.IsEnabled;
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(LdapConfigurationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var config = await _context.LdapConfigurations.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new LdapConfiguration();
            _context.LdapConfigurations.Add(config);
        }

        config.Host = model.Host;
        config.Port = model.Port;
        config.BaseDn = model.BaseDn;
        config.AdminDn = model.AdminDn;
        config.AdminPassword = model.AdminPassword;
        config.UseSsl = model.UseSsl;
        config.IsEnabled = model.IsEnabled;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "LDAP Configuration saved successfully.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Sync()
    {
        var model = new LdapSyncViewModel { IsSyncPerformed = true };
        
        try
        {
            var ldapUsers = await _ldapService.GetAllUsersAsync();
            int created = 0;
            int updated = 0;

            foreach (var ldapUser in ldapUsers)
            {
                var user = await _userManager.FindByNameAsync(ldapUser.UserName);
                var status = "Existing";
                
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = ldapUser.UserName,
                        Email = ldapUser.Email,
                        FullName = ldapUser.FullName,
                        IsActive = true,
                        IsLdapUser = true,
                        LdapDistinguishedName = ldapUser.DistinguishedName,
                        EmailConfirmed = true
                    };
                    
                    var result = await _userManager.CreateAsync(user, "LdapUser@123!"); 
                    
                    if (result.Succeeded)
                    {
                        created++;
                        status = "Created";
                        await _publishEndpoint.Publish<IUserCreated>(new
                        {
                            UserId = user.Id,
                            Email = user.Email,
                            UserName = user.UserName,
                            FullName = user.FullName,
                            IsActive = user.IsActive
                        });
                    }
                    else
                    {
                        status = "Error: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    }
                }
                else
                {
                     updated++;
                }

                model.Users.Add(new LdapUserPreview
                {
                    UserName = ldapUser.UserName,
                    Email = ldapUser.Email,
                    FullName = ldapUser.FullName,
                    Status = status
                });
            }
            
            model.Message = $"Sync completed. Created: {created}, Existing: {updated}";
        }
        catch (Exception ex)
        {
             model.Message = $"Sync failed: {ex.Message}";
             _logger.LogError(ex, "LDAP Sync failed");
        }

        return View("Index", model);
    }
}
