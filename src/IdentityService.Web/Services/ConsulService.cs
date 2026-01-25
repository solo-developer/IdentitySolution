using Consul;

namespace IdentityService.Web.Services;

public interface IConsulService
{
    Task<List<ServiceInfo>> GetRegisteredServicesAsync();
    Task<List<string>> GetAllModulesAsync();
}

public class ConsulService : IConsulService
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulService> _logger;
    private readonly IdentityService.Application.Interfaces.IApplicationDbContext _context;

    public ConsulService(
        IConsulClient consulClient, 
        ILogger<ConsulService> logger,
        IdentityService.Application.Interfaces.IApplicationDbContext context)
    {
        _consulClient = consulClient;
        _logger = logger;
        _context = context;
    }

    public async Task<List<ServiceInfo>> GetRegisteredServicesAsync()
    {
        try
        {
            var services = await _consulClient.Agent.Services();
            var serviceInfoList = new List<ServiceInfo>();

            foreach (var service in services.Response.Values)
            {
                // Get health check status
                var healthChecks = await _consulClient.Health.Checks(service.Service);
                var status = healthChecks.Response.Any() 
                    ? healthChecks.Response.First().Status.ToString() 
                    : "unknown";

                // Get service metadata if available
                var metadata = service.Meta ?? new Dictionary<string, string>();

                serviceInfoList.Add(new ServiceInfo
                {
                    Id = service.ID,
                    Name = service.Service,
                    Address = service.Address,
                    Port = service.Port,
                    Status = status,
                    Tags = service.Tags?.ToList() ?? new List<string>(),
                    Metadata = metadata.ToDictionary<string,string>(),
                    Module = metadata.ContainsKey("Module") ? metadata["Module"] : service.Service
                });
            }

            return serviceInfoList.OrderBy(s => s.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving services from Consul");
            return new List<ServiceInfo>();
        }
        }

    public async Task<List<string>> GetAllModulesAsync()
    {
        // 1. Get modules from Consul Catalog (Cluster-wide Services)
        // Using Catalog.Services() instead of Agent.Services() to see everything
        var services = await _consulClient.Catalog.Services();
        var consulModules = services.Response.Keys
            .Where(k => !string.Equals(k, "consul", StringComparison.OrdinalIgnoreCase) && 
                        !string.Equals(k, "IdentityService", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 2. Get historical modules from DB
        var dbModules = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
             _context.Permissions.Select(p => p.Module).Distinct());

        // 3. Merge
        return consulModules.Union(dbModules)
            .Distinct()
            .OrderBy(m => m)
            .ToList();
    }
}

public class ServiceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string Module { get; set; } = string.Empty;
}
