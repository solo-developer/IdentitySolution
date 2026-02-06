namespace IdentityService.Web.Configurations;

public class LdapSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = string.Empty;
    public string AdminDn { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = false;
}
