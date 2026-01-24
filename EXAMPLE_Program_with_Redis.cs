using IdentityService.Infrastructure;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IdentitySolution.ServiceDiscovery;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);

// Shared Data Protection for cross-service authentication
// PRODUCTION: Use Redis for distributed systems
if (builder.Environment.IsProduction())
{
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"] 
        ?? throw new InvalidOperationException("Redis connection string is required in production");
    
    var redis = ConnectionMultiplexer.Connect(redisConnectionString);
    
    builder.Services.AddDataProtection()
        .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
        .SetApplicationName("IdentitySolution");
}
else
{
    // DEVELOPMENT: Use file-based storage (single machine only)
    var sharedKeysPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "SharedKeys");
    Directory.CreateDirectory(sharedKeysPath);
    
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(sharedKeysPath))
        .SetApplicationName("IdentitySolution");
}

// Add Consul
builder.Services.AddConsulConfig(builder.Configuration);
builder.Services.AddScoped<IConsulService, ConsulService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseConsul();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();
app.MapRazorPages();
app.MapHealthChecks("/health");

app.Run();
