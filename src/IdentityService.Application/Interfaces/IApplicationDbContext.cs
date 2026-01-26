using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace IdentityService.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Module> Modules { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
