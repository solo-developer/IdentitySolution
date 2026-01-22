namespace IdentitySolution.Shared.Events;

public interface IUserUpdated
{
    string UserId { get; }
    string Email { get; }
    string UserName { get; }
}
