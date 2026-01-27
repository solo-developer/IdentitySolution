using Consul;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IdentitySolution.ServiceDiscovery;

public static class ServiceDiscoveryHelper
{
    public static async Task<string> GetServiceAddressAsync(IConfiguration configuration, string serviceName, string scheme = "https")
    {
        var consulAddress = configuration["Consul:Address"] ?? "http://localhost:8500";
        
        using var client = new ConsulClient(config =>
        {
            config.Address = new Uri(consulAddress);
        });

        // Retry logic could be added here if startup resilience is needed
        // For now, we try once as this is called during startup configuration
        var services = await client.Health.Service(serviceName, tag: null, passingOnly: true);
        
        if (services.Response != null && services.Response.Length > 0)
        {
            var serviceEntry = services.Response.First();
            var host = serviceEntry.Service.Address;
            var port = serviceEntry.Service.Port;

            // Handle cases where address might be 0.0.0.0 if registered inside docker but accessed from outside
            // This logic mirrors HostExtensions.cs but simplified
            if (string.IsNullOrEmpty(host) || host == "0.0.0.0" || host == "::" || host == "[::]")
            {
                host = "localhost";
            }

            return $"{scheme}://{host}:{port}";
        }

        return null; // OR throw exception
    }
}
