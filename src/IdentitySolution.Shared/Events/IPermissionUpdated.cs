namespace IdentitySolution.Shared.Events;

public interface IPermissionUpdated
{
    Guid PermissionId { get; }
    string Name { get; }
    string Description { get; }
    string Module { get; }
    bool IsActive { get; }
}
