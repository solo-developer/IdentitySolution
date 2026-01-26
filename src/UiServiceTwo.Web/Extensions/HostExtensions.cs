using Consul;
using Microsoft.Extensions.Logging;
using Polly;
using System.Net.Sockets;
using UiServiceTwo.Web.Services;

namespace UiServiceTwo.Web.Extensions;

public static class HostExtensions
{
    public static void StartWaitForIdentityServiceInBackground(this IApplicationBuilder app)
    {
        // Fire and forget to allow parsing Pipeline to finish and IIS to see the app as "Running"
        Task.Run(async () =>
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<Program>>();
            
            try 
            {
                 var startupStatus = app.ApplicationServices.GetRequiredService<StartupStatus>();
                 var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();
                 var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();

                 const string IdentityServiceName = "IdentityService"; 

                 logger.LogInformation("Looking up IdentityService in Consul...");

                 var discoveryPolicy = Policy
                    .HandleResult<ServiceEntry[]>(services => services == null || services.Length == 0)
                    .WaitAndRetryAsync(
                        retryCount: 10,
                        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(3),
                        onRetry: (delegateResult, timeSpan, retryCount, context) =>
                        {
                            logger.LogInformation($"IdentityService not found in Consul yet... (Attempt {retryCount}/10)");
                        });

                 string? host = null;
                 int port = 0;
                 bool resolved = false;

                 // 1. Wait for Service Discovery (Consul)
                 var result = await discoveryPolicy.ExecuteAsync(async () =>
                 {
                    try {
                        var services = await consulClient.Health.Service(IdentityServiceName, tag: null, passingOnly: true);
                        return services.Response;
                    } catch { return null; }
                 });

                 if (result != null && result.Length > 0)
                 {
                    var serviceEntry = result[0];
                    host = serviceEntry.Service.Address;
                    port = serviceEntry.Service.Port;
            
                    if (string.IsNullOrEmpty(host) || host == "0.0.0.0") host = "localhost";
                    
                    logger.LogInformation($"Found IdentityService via Consul at {host}:{port}");
                    resolved = true;
                 }
                 else
                 {
                    // FALLBACK: Try Configured Authority URL
                    logger.LogWarning("Consul lookup failed. Falling back to 'IdentityService:Authority' configuration...");
                    var authority = configuration["IdentityService:Authority"];
                    if (!string.IsNullOrEmpty(authority) && Uri.TryCreate(authority, UriKind.Absolute, out var uri))
                    {
                        host = uri.Host;
                        port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);
                        logger.LogInformation($"Resolved IdentityService from Config: {host}:{port}");
                        resolved = true;
                    }
                    else
                    {
                        logger.LogError("Could not resolve IdentityService from Consul OR Config.");
                    }
                 }

                 if (resolved)
                 {
                     // 2. Wait for TCP Reachability
                     var tcpPolicy = Policy
                       .Handle<Exception>()
                       .WaitAndRetryAsync(
                           retryCount: 20, 
                           sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(2),
                           onRetry: (exception, timeSpan, retryCount, context) =>
                           {
                               logger.LogInformation($"Waiting for TCP connection to IdentityService at {host}:{port}... (Attempt {retryCount}/20)");
                           });

                     await tcpPolicy.ExecuteAsync(async () =>
                     {
                        using var tcpClient = new TcpClient();
                        var connectTask = tcpClient.ConnectAsync(host!, port);
                
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
                     
                     // SIGNAL READY
                     startupStatus.IsReady = true;
                     logger.LogInformation("Dependency Check Passed. Application is now accepting requests.");
                 }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical failure waiting for IdentityService.");
            }
        });
    }
}
