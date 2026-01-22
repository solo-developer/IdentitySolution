using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;
using System.Linq;
using MassTransit;
using IdentitySolution.Shared.Events;
using IdentityService.Domain.Constants;

namespace IdentityService.Api.Controllers;

public class AuthorizationController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IPublishEndpoint _publishEndpoint;

    public AuthorizationController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IOpenIddictApplicationManager applicationManager,
        IPublishEndpoint publishEndpoint)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _applicationManager = applicationManager;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Retrieve the user principal stored in the authentication cookie.
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        // If the user principal is not available, redirect the user to the login page.
        if (!result.Succeeded)
        {
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.Query.Where(parameter => parameter.Key != OpenIddictConstants.Parameters.Prompt).ToList())
                });
        }

        // Retrieve the profile of the logged in user.
        var user = await _userManager.GetUserAsync(result.Principal) ??
            throw new InvalidOperationException("The user details cannot be retrieved.");

        // Create a new ClaimsPrincipal containing the claims that will be used to create an id_token, a token or a code.
        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        // Ensure the subject claim is present, as it is mandatory for OpenIddict.
        var identity = (ClaimsIdentity)principal.Identity!;
        if (!principal.HasClaim(c => c.Type == OpenIddictConstants.Claims.Subject))
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user)));
        }

        // Set the list of scopes granted to the client application.
        principal.SetScopes(request.GetScopes());
        principal.SetResources("identity-server");

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            // Retrieve the claims principal stored in the authorization code/refresh token.
            var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;

            // Retrieve the user profile corresponding to the specified principal.
            var user = await _userManager.GetUserAsync(principal);
            if (user == null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }));
            }

            // Ensure the user is still allowed to log in.
            if (!await _signInManager.CanSignInAsync(user))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                    }));
            }

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        else if (request.IsClientCredentialsGrantType())
        {
            var application = await _applicationManager.FindByClientIdAsync(request.ClientId) ??
                throw new InvalidOperationException("The application cannot be found.");

            // Create a new ClaimsIdentity containing the claims that will be used to create an id_token, a token or a code.
            var identity = new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                OpenIddictConstants.Claims.Name,
                OpenIddictConstants.Claims.Role);

            // Use the client_id as the subject identifier.
            var subjectClaim = new Claim(OpenIddictConstants.Claims.Subject, await _applicationManager.GetClientIdAsync(application));
            subjectClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(subjectClaim);

            var nameClaim = new Claim(OpenIddictConstants.Claims.Name, await _applicationManager.GetDisplayNameAsync(application));
            nameClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(nameClaim);

            // Note: In a real world scenario, you'd likely want to have a more robust system for assigning roles to clients.
            // For this sample recovery project, we'll assign the Administrator role to allow access to UserManagement.
            if (await _applicationManager.GetClientIdAsync(application) == "recovery-project")
            {
                var roleClaim = new Claim(OpenIddictConstants.Claims.Role, Roles.Administrator);
                roleClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
                identity.AddClaim(roleClaim);
            }

            var principal = new ClaimsPrincipal(identity);

            principal.SetScopes(request.GetScopes());
            principal.SetResources("identity-server");

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            await _publishEndpoint.Publish<IUserLoggedOut>(new
            {
                UserId = user.Id,
                UserName = user.UserName
            });
        }

        await _signInManager.SignOutAsync();

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties
            {
                RedirectUri = "/"
            });
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (principal.HasScope(OpenIddictConstants.Scopes.Profile))
                    yield return OpenIddictConstants.Destinations.IdentityToken;

                yield break;

            case OpenIddictConstants.Claims.Email:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (principal.HasScope(OpenIddictConstants.Scopes.Email))
                    yield return OpenIddictConstants.Destinations.IdentityToken;

                yield break;

            case OpenIddictConstants.Claims.Role:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (principal.HasScope(OpenIddictConstants.Scopes.Roles))
                    yield return OpenIddictConstants.Destinations.IdentityToken;

                yield break;

            // Never include the security stamp in the access and identity tokens, as it's a secret value.
            case "AspNet.Identity.SecurityStamp": yield break;

            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }
}
