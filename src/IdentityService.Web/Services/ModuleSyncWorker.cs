using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Web.Services;

public class ModuleSyncWorker : BackgroundService
{
    private readonly ILogger<ModuleSyncWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(1);

    public ModuleSyncWorker(ILogger<ModuleSyncWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ModuleSyncWorker starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncModulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing modules from Consul.");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("ModuleSyncWorker stopping.");
    }

    private async Task SyncModulesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var consulService = scope.ServiceProvider.GetRequiredService<IConsulService>();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        // 1. Get all services from Consul
        var services = await consulService.GetRegisteredServicesAsync();
        
        // We only care about distinct Module names
        var activeModules = services
            .Where(s => !string.Equals(s.Name, "consul", StringComparison.OrdinalIgnoreCase)) // Skip internal consul service
            .Select(s => s.Module)
            .Distinct()
            .ToList();

        if (!activeModules.Any()) return;

        // 2. Get existing modules from DB
        var existingModules = await context.Modules.ToListAsync(stoppingToken);

        var newModulesCount = 0;

        foreach (var moduleName in activeModules)
        {
            if (string.IsNullOrWhiteSpace(moduleName)) continue;

            var existing = existingModules.FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));
            
            if (existing == null)
            {
                // New module found
                _logger.LogInformation("Found new module in Consul: {ModuleName}. Registering...", moduleName);
                
                var newModule = new Module
                {
                    Name = moduleName,
                    Description = $"Automatically registered module for {moduleName}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Modules.Add(newModule);
                newModulesCount++;
            }
            else
            {
                // Optional: Update timestamp or status if needed
                // For now, just ensure it's active
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    // context.Entry(existing).State = EntityState.Modified; // If tracking issues, but filtered list is from context so it is tracked.
                }
            }
        }

        if (newModulesCount > 0)
        {
             await context.SaveChangesAsync(stoppingToken);
             _logger.LogInformation("Registered {Count} new modules.", newModulesCount);
        }
    }
}
