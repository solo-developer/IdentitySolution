using System;
using System.Collections.Generic;

namespace IdentitySolution.Shared.Events;

public interface IRoleUpdated
{
    string RoleId { get; }
    string Name { get; }
    string Module { get; }
    List<string> Permissions { get; }
}
