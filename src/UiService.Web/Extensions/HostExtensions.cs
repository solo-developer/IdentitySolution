using Consul;
using Microsoft.Extensions.Logging;
using Polly;
using System.Net.Sockets;
using UiService.Web.Services;

namespace UiService.Web.Extensions;

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

                 var maxRetryTime = TimeSpan.FromMinutes(10);
                 var startedAt = DateTime.UtcNow;

                 string? host = null;
                 int port = 0;
                 bool resolved = false;

                 while (!resolved && (DateTime.UtcNow - startedAt) < maxRetryTime)
                 {
                     // 1. Try Service Discovery (Consul)
                     // Configuration override removed as per requirements - strictly using Consul resolution.

                     
                     // 2. Try Service Discovery (Consul) if config didn't give a result OR we want to verify it
                     if (!resolved)
                     {
                         try 
                         {
                             var services = await consulClient.Health.Service(IdentityServiceName, tag: null, passingOnly: true);
                             if (services.Response != null && services.Response.Length > 0)
                             {
                                 var serviceEntry = services.Response[0];
                                 host = serviceEntry.Service.Address;
                                 port = serviceEntry.Service.Port;
                        
                                 // Fix for Docker/Consul binding to 0.0.0.0
                                 if ((string.IsNullOrEmpty(host) || host == "0.0.0.0" || host == "::" || host == "[::]") && 
                                     app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsDevelopment()) 
                                 {
                                     host = "localhost";
                                 }
                                 
                                 logger.LogInformation($"Found IdentityService via Consul at {host}:{port}");
                                 resolved = true;
                             }
                         } 
                         catch (Exception ex) 
                         { 
                             logger.LogWarning($"Consul lookup failed: {ex.Message}");
                         }
                     }

                     if (!resolved)
                     {
                         logger.LogInformation("IdentityService not found in Config OR Consul. Retrying in 3 seconds...");
                         await Task.Delay(3000);
                     }
                 }

                 if (resolved)
                 {
                     // 2. Wait for TCP Reachability
                     var tcpPolicy = Polly.Policy
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
                 else
                 {
                     logger.LogError("Could not resolve IdentityService after maximum retry time.");
                 }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical failure waiting for IdentityService.");
            }
        });
    }
}
