namespace IdentitySolution.Shared.Events;

public interface IPermissionCreated
{
    Guid PermissionId { get; }
    string Name { get; }
    string Description { get; }
    string Module { get; }
    bool IsActive { get; }
    Guid? ParentId { get; }
}
