# Identity Solution & User Management System

This repository provides a centralized **Identity and Access Management (IAM)** solution built with **OpenIddict**, **ASP.NET Core Identity**, and **MassTransit**. It supports **Single Sign-On (SSO)**, **Single Logout (SLO)**, and **Service Discovery** via Consul.

---

## 🏗️ Architecture Overview

- **IdentityService.Api**: The central OpenID Connect (OIDC) provider. Handles authentication, token issuance, and user management.
- **Service Discovery (Consul)**: Used for dynamic registration of modules and services.
- **Event Bus (RabbitMQ)**: Facilitates real-time communication (e.g., Global Logout events).
- **Client Applications**: Standard ASP.NET Core MVC/Razor apps or Machine-to-Machine services.

---

## 🚀 Connecting a New Service

To integrate a new service with the Identity Solution, follow these steps:

### 1. Register the Client in Identity Service
Open `src/IdentityService.Infrastructure/Persistence/DatabaseInitializer.cs` and add a new `OpenIddictApplicationDescriptor`:

```csharp
new OpenIddictApplicationDescriptor
{
    ClientId = "your-app-id",
    ClientSecret = "your-app-secret",
    DisplayName = "Your Application Name",
    ClientType = OpenIddictConstants.ClientTypes.Confidential,
    RedirectUris = { new Uri("https://localhost:PORT/signin-oidc") },
    PostLogoutRedirectUris = { new Uri("https://localhost:PORT/signout-callback-oidc") },
    FrontChannelLogoutUri = new Uri("https://localhost:PORT/signout-oidc"),
    Permissions = {
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Logout,
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
        OpenIddictConstants.Permissions.ResponseTypes.Code,
        OpenIddictConstants.Permissions.Scopes.Email,
        OpenIddictConstants.Permissions.Scopes.Profile,
        OpenIddictConstants.Permissions.Scopes.Roles,
        OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
        OpenIddictConstants.Permissions.Prefixes.Scope + "api"
    }
}
```

### 2. Configure OIDC Authentication (Web Apps)
In your client's `Program.cs`, add the following:

```csharp
builder.Services.AddAuthentication(options => {
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options => {
    options.Cookie.Name = "YourApp.Cookie";
})
.AddOpenIdConnect(options => {
    options.Authority = "https://localhost:7242"; // Identity Service URL
    options.ClientId = "your-app-id";
    options.ClientSecret = "your-app-secret";
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Scope.Add("roles");
});
```

---

## 🔄 Global Logout (Single Logout - SLO)

To ensure that logging out of one application signs the user out of all applications, we use a combined **Event-Driven** and **Middleware** approach.

### 1. Subscribe to the Logout Event
Your service must consume the `IUserLoggedOut` event published by the Identity Service.

**Consumer Implementation:**
```csharp
public class UserLoggedOutConsumer : IConsumer<IUserLoggedOut> {
    private readonly IGlobalSessionStore _sessionStore;
    public async Task Consume(ConsumeContext<IUserLoggedOut> context) {
        _sessionStore.InvalidateUser(context.Message.UserId);
    }
}
```

### 2. Register Global Session Middleware
The middleware checks every request to see if the authenticated user has been marked as "logged out" globally.

```csharp
// Program.cs
app.UseMiddleware<GlobalLogoutMiddleware>();
app.UseAuthentication();
```

---

## 🖥️ Service Discovery & Workers

The solution uses a worker to automatically register itself as a "Module" in the management system upon startup.

### 1. Register the Service Discovery Module
In `Program.cs`:
```csharp
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddScoped<IModuleRegistrationService, ModuleRegistrationService>();
builder.Services.AddHostedService<ServiceRegistrationWorker>();
```

### 2. The ServiceRegistrationWorker
This worker runs on startup, gathers information about the current service (Port, IP, Name), and sends a registration event to the Identity Service via RabbitMQ.

---

## 📩 List of Available Events

| Event Name | Source | Purpose |
| :--- | :--- | :--- |
| `IUserLoggedOut` | Identity Service | Broadcasts when a user logs out globally. |
| `IRegisterModule` | Client Services | Sent on startup to register the service in the central registry. |
| `IUserCreated` | Identity Service | Broadcasts when a new user is registered. |

---

## 🛠️ Technical Details & Troubleshooting

### Data Protection Keys
For SSO to work on `localhost`, all services must share a stable encryption context. In development, the Identity Service saves keys to `C:\temp\IdentitySolution_Keys`. Ensure this directory exists and is writable.

### Common Issues
1. **Invalid Scope Error**: Ensure the `roles` and `api` scopes are created in the database. The `DatabaseInitializer` handles this automatically on startup.
2. **SSO Not Working**: 
   - Ensure both apps are using the same OIDC `Authority`.
   - Check that browser cookies for `localhost` are not being blocked.
   - Use the same browser profile for both apps.

---

## 📖 Recommended File Structure
- `src/IdentityService.Api`: Central Identity Provider.
- `src/IdentitySolution.Shared`: Common models and event interfaces.
- `src/IdentitySolution.ServiceDiscovery`: Consul integration logic.
