# Identity Solution & User Management System

This repository provides a centralized **Identity and Access Management (IAM)** solution built with **OpenIddict**, **ASP.NET Core Identity**, and **MassTransit**. It supports **Single Sign-On (SSO)**, **Single Logout (SLO)**, and **Service Discovery** via Consul.

---

## 🏗️ Architecture Overview

- **IdentityService.Api**: The central OpenID Connect (OIDC) provider. Handles authentication, token issuance, and user management.
- **Service Discovery (Consul)**: Used for dynamic registration of modules and services.
- **Event Bus (RabbitMQ)**: Facilitates real-time communication (e.g., Global Logout events).
- **Client Applications**: Standard ASP.NET Core MVC/Razor apps or Machine-to-Machine services.

---

## 🚀 Connecting a New Service (Zero-Touch Integration)

Starting with the latest update, you no longer need to modify the **Identity Service (Core)** or **Infrastructure** code to add a new service. New services can now self-register their OIDC clients, roles, and permissions dynamically using settings from their `appsettings.json`.

### 1. Configure your `appsettings.json`
Add the following sections to your client service to define its identity and discovery parameters:

```json
"IdentityClient": {
  "ClientId": "your-app-id",
  "ClientSecret": "your-app-secret",
  "BaseUrl": "https://localhost:PORT"
},
"Consul": {
  "Address": "http://localhost:8500",
  "ServicePort": PORT,
  "ServiceName": "Your.Service.Name"
}
```

### 2. Implement the ServiceRegistrationWorker
The worker now automatically builds OIDC URLs and health check endpoints using the configuration provided above.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var configSection = _configuration.GetSection("IdentityClient");
    var baseUrl = configSection["BaseUrl"];

    var oidcClients = new List<OidcClientDto>
    {
        new OidcClientDto
        {
            ClientId = configSection["ClientId"],
            ClientSecret = configSection["ClientSecret"],
            DisplayName = _configuration["ServiceName"],
            RedirectUris = { $"{baseUrl}/signin-oidc" },
            PostLogoutRedirectUris = { $"{baseUrl}/signout-callback-oidc" },
            FrontChannelLogoutUri = $"{baseUrl}/signout-oidc",
            HealthCheckUrl = $"{baseUrl}/health"
        }
    };

    using (var scope = _scopeFactory.CreateScope())
    {
        var regService = scope.ServiceProvider.GetRequiredService<IModuleRegistrationService>();
        await regService.RegisterAsync(roles, permissions, users, oidcClients);
    }
}
```

### 3. Enable Health Checks
All services should expose a health endpoint to allow Consul to monitor their status.
In `Program.cs`:
```csharp
builder.Services.AddHealthChecks();
// ...
app.MapHealthChecks("/health");
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

## 🏥 Health Checks & Monitoring

The system uses standard ASP.NET Core Health Checks integrated with Consul:
- **Identity Service**: Monitors database connectivity (`/health`).
- **UI Services**: Monitor basic availability and self-register their health status.
- **Consul**: Automatically pings each service's health endpoint. Check the status at `http://localhost:8500`.

---

## 🖥️ Service Discovery

Services automatically register with Consul on startup. The registration includes:
- **Service Name** and **Port** (from `appsettings.json`).
- **Health Check URI**: Standardized as `/health`.
- **Automatic Deregistration**: Services are removed from Consul 1 minute after failing health checks.

---

## 📩 List of Available Events

| Event Name | Source | Purpose |
| :--- | :--- | :--- |
| `IUserLoggedOut` | Identity Service | Broadcasts when a user logs out globally. |
| `IRegisterModule` | Client Services | Sent on startup to register the service and its OIDC client. |
| `IUserCreated` | Identity Service | Broadcasts when a new user is registered. |

---

## 🛠️ Technical Details & Troubleshooting

### Data Protection Keys
For SSO to work on `localhost`, all services must share a stable encryption context. In development, the Identity Service saves keys to `C:\temp\IdentitySolution_Keys`. Ensure this directory exists and is writable.

### Common Issues
1. **Consul Connection**: Ensure the Consul agent is running (default `http://localhost:8500`).
2. **SSO Not Working**: 
   - Check if the `BaseUrl` in `appsettings.json` matches your actual running URL exactly (including `https`).
   - Use the same browser profile for both apps.

---

## 📖 Project Structure
- `src/IdentityService.Api`: Central Identity Provider + Health Checks.
- `src/IdentitySolution.Shared`: DTOs and Event Interfaces.
- `src/IdentitySolution.ServiceDiscovery`: Consul extensions and registration logic.
