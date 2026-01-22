using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentitySolution.ServiceDiscovery;

public static class ConsulExtensions
{
    public static IServiceCollection AddConsulConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
        {
            var address = configuration["Consul:Address"] ?? "http://localhost:8500";
            consulConfig.Address = new Uri(address);
        }));

        return services;
    }

    public static IApplicationBuilder UseConsul(this IApplicationBuilder app)
    {
        var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("ConsulRegistration");
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        var registration = new AgentServiceRegistration()
        {
            ID = Guid.NewGuid().ToString(),
            Name = app.ApplicationServices.GetRequiredService<IConfiguration>()["Consul:ServiceName"] ?? "UnknownService",
            Address = "localhost", // Should be dynamic in production
            Port = 7200, // Should be dynamic
        };

        try
        {
            logger.LogInformation("Registering with Consul");
            // consulClient.Agent.ServiceDeregister(registration.ID).Wait(); // No need to deregister a random new GUID
            consulClient.Agent.ServiceRegister(registration).Wait();

            lifetime.ApplicationStopping.Register(() =>
            {
                logger.LogInformation("Unregistering from Consul");
                consulClient.Agent.ServiceDeregister(registration.ID).Wait();
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Could not register with Consul: {ex.Message}. Continuing without service discovery.");
        }

        return app;
    }
}
