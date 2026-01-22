namespace IdentitySolution.Shared.Events;

public interface IUserLoggedOut
{
    string UserId { get; }
    string UserName { get; }
}
