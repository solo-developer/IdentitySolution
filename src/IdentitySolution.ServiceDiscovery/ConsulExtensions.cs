using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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

        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var serviceName = config["Consul:ServiceName"] ?? "UnknownService";
        var servicePort = int.Parse(config["Consul:ServicePort"] ?? "7200");
        var baseUrl = config["IdentityClient:BaseUrl"] ?? $"https://localhost:{servicePort}";

        var registration = new AgentServiceRegistration()
        {
            ID = Guid.NewGuid().ToString(),
            Name = serviceName,
            Address = "localhost",
            Port = servicePort,
            Check = new AgentServiceCheck()
            {
                HTTP = $"{baseUrl}/health",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                TLSSkipVerify = true // Since we are using localhost self-signed certs
            }
        };

        lifetime.ApplicationStarted.Register(() =>
        {
            // Run registration in background to avoid blocking startup
            Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Registering {ServiceName} on port {ServicePort} with Consul", serviceName, servicePort);
                    await consulClient.Agent.ServiceRegister(registration);
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Could not register with Consul: {ex.Message}. Continuing without service discovery.");
                }
            });
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                logger.LogInformation("Unregistering from Consul");
                consulClient.Agent.ServiceDeregister(registration.ID).Wait();
            }
            catch (Exception ex)
            {
                 logger.LogWarning($"Could not unregister from Consul: {ex.Message}");
            }
        });

        return app;
    }
}
