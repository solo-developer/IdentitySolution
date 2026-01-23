
namespace IdentitySolution.Shared.Events;

public interface IUserLoggedIn
{
    Guid UserId { get; }
    string UserName { get; }
}
