using IdentitySolution.Shared.Models;

namespace IdentitySolution.Shared.Events;

public interface IRegisterModule
{
    string ModuleName { get; }
    List<RoleDto> Roles { get; }
    List<PermissionDto> Permissions { get; }
    List<UserDto> Users { get; }
}
