using System.ComponentModel.DataAnnotations;

namespace IdentityService.Web.ViewModels;

public class LdapConfigurationViewModel
{
    public int Id { get; set; }
    
    [Required]
    public string Host { get; set; } = string.Empty;
    
    [Required]
    public int Port { get; set; } = 389;
    
    [Required]
    public string BaseDn { get; set; } = string.Empty;
    
    [Required]
    public string AdminDn { get; set; } = string.Empty;
    
    [Required]
    [DataType(DataType.Password)]
    public string AdminPassword { get; set; } = string.Empty;
    
    public bool UseSsl { get; set; }
    
    public bool IsEnabled { get; set; } = true;
}
