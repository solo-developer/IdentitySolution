using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

public class LdapConfiguration
{
    [Key]
    public int Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = string.Empty;
    public string AdminDn { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty; // In a real app, encrypt this!
    public bool UseSsl { get; set; }
    public bool IsEnabled { get; set; }
}
