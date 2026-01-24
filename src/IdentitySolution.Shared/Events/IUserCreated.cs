namespace IdentitySolution.Shared.Events;

public interface IUserCreated
{
    string UserId { get; }
    string Email { get; }
    string UserName { get; }
    string FullName { get; }
    bool IsActive { get; }
}
