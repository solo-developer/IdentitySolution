using Consul;
using Microsoft.Extensions.Logging;
using Polly;
using System.Net.Sockets;

namespace UiServiceTwo.Web.Extensions;

public static class HostExtensions
{
    public static async Task WaitForIdentityServiceAsync(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<Program>>();
        var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();

        const string IdentityServiceName = "IdentityService"; // Must match what IdentityService registers as

        logger.LogInformation("Looking up IdentityService in Consul...");

        var discoveryPolicy = Polly.Policy
            .HandleResult<ServiceEntry[]>(services => services == null || services.Length == 0)
            .WaitAndRetryAsync(
                retryCount: 20,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(3),
                onRetry: (delegateResult, timeSpan, retryCount, context) =>
                {
                    logger.LogInformation($"IdentityService not found in Consul yet... (Attempt {retryCount}/20)");
                });

        string? host = null;
        int port = 0;

        // 1. Wait for Service Discovery (Consul)
        var result = await discoveryPolicy.ExecuteAsync(async () =>
        {
            var services = await consulClient.Health.Service(IdentityServiceName, tag: null, passingOnly: true);
            return services.Response;
        });

        if (result != null && result.Length > 0)
        {
            var serviceEntry = result[0];
            host = serviceEntry.Service.Address;
            port = serviceEntry.Service.Port;
            
            // Fallback for localhost if address is empty/0.0.0.0
            if (string.IsNullOrEmpty(host) || host == "0.0.0.0") host = "localhost";
            
            logger.LogInformation($"Found IdentityService via Consul at {host}:{port}");
        }
        else
        {
            logger.LogWarning("Could not resolve IdentityService from Consul after retries. Aborting wait.");
            return;
        }

        // 2. Wait for TCP Reachability
        var tcpPolicy = Polly.Policy
           .Handle<Exception>()
           .WaitAndRetryAsync(
               retryCount: 10, 
               sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(2),
               onRetry: (exception, timeSpan, retryCount, context) =>
               {
                   logger.LogInformation($"Waiting for TCP connection to IdentityService at {host}:{port}... (Attempt {retryCount}/10)");
               });

        await tcpPolicy.ExecuteAsync(async () =>
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            
            if (await Task.WhenAny(connectTask, Task.Delay(2000)) != connectTask)
            {
                throw new TimeoutException($"Connection to IdentityService timed out.");
            }
            
            if (!tcpClient.Connected)
            {
                 throw new Exception($"Could not connect to IdentityService.");
            }
            
            logger.LogInformation($"Successfully connected to IdentityService at {host}:{port}!");
        });
    }
}
