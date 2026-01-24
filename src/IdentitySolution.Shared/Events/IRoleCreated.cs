using System.Collections.Generic;

namespace IdentitySolution.Shared.Events;

public interface IRoleCreated
{
    string RoleId { get; }
    string Name { get; }
    string Description { get; }
    string Module { get; }
}
