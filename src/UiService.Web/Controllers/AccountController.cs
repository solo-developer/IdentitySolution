using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace UiService.Web.Controllers;

public class AccountController : Controller
{
    public IActionResult Login(string returnUrl = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        // Logout from local application cookie
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // Logout from Identity Service (OIDC)
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public IActionResult SilentLogin()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action("SilentLoginCallback"),
        };
        // Signal to OIDC middleware to Use "prompt=none"
        // Note: The OpenIdConnect middleware looks for "prompt" in the Items dictionary if configured via events, 
        // but typically we pass it as a parameter to the Challenge.
        // However, the standard Challenge method doesn't easily let us set OIDC protocol parameters directly into the QueryString 
        // unless we use the OnRedirectToIdentityProvider event. 
        // A common convention for manual overrides is setting it in Items.
        props.Items["prompt"] = "none";
        
        return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public IActionResult SilentLoginCallback()
    {
        return View();
    }
}
