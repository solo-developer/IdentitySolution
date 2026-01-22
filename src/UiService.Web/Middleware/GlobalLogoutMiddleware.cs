using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using UiService.Web.Services;

namespace UiService.Web.Middleware;

public class GlobalLogoutMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalLogoutMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGlobalSessionStore sessionStore)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? context.User.FindFirst("sub")?.Value;

            if (userId != null && sessionStore.IsUserInvalid(userId))
            {
                // Sign out locally
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Redirect to home or login
                context.Response.Redirect("/");
                return;
            }
        }

        await _next(context);
    }
}
