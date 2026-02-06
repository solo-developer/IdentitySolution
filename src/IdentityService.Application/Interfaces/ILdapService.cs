using IdentityService.Domain.Entities;

namespace IdentityService.Application.Interfaces;

public interface ILdapService
{
    Task<List<LdapUser>> GetAllUsersAsync();
    Task<bool> ValidateCredentialsAsync(string username, string password);
}

public class LdapUser
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
}
