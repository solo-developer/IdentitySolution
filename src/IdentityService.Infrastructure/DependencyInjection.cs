using System.IO;
using Microsoft.AspNetCore.DataProtection;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using IdentitySolution.Shared.Events;
using IdentitySolution.ServiceDiscovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            
            // Register OpenIddict entities
            options.UseOpenIddict();
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<ApplicationClaimsPrincipalFactory>();

        // Configure OpenIddict
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<ApplicationDbContext>();
            })
            .AddServer(options =>
            {
                // Enable endpoints
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetUserinfoEndpointUris("/connect/userinfo")
                       .SetLogoutEndpointUris("/connect/logout");

                // Enable flows
                options.AllowAuthorizationCodeFlow()
                       .AllowClientCredentialsFlow()
                       .RequireProofKeyForCodeExchange(); // PKCE

                // Encryption and signing credentials
                // In production, use X.509 certificates loaded from file or store
                // options.AddDevelopmentEncryptionCertificate()
                //        .AddDevelopmentSigningCertificate();
                
                // USE EPHEMERAL KEYS TO FIX IIS 'ACCESS DENIED' ERROR
                // Note: This means tokens are invalidated on application restart.
                options.AddEphemeralEncryptionKey()
                       .AddEphemeralSigningKey();

                // Register ASP.NET Core host
                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableLogoutEndpointPassthrough();

                // Custom claim mapping to ensure the 'sub' claim is correctly handled
                // options.AddClaims is not available in this version or takes different arguments. 
                // We are already adding the subject claim manually in the AuthorizationController and ClaimsFactory.
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<DatabaseInitializer>();
        
        // Messaging Configuration
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<IdentityService.Infrastructure.Consumers.RegisterModuleConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqHost = configuration["RabbitMq:Host"] ?? "localhost";
                cfg.Host(rabbitMqHost, "/", h =>
                {
                    h.Username(configuration["RabbitMq:Username"] ?? "guest");
                    h.Password(configuration["RabbitMq:Password"] ?? "guest");
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddConsulConfig(configuration);

        return services;
    }
}
