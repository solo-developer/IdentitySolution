using IdentityService.Web.ViewModels;

namespace IdentityService.Web.ViewModels;

public class LdapSyncViewModel
{
    public List<LdapUserPreview> Users { get; set; } = new();
    public int NewUsersCount => Users.Count(u => u.Status == "New");
    public int ExistingUsersCount => Users.Count(u => u.Status == "Existing");
    public bool IsSyncPerformed { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class LdapUserPreview
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string Status { get; set; } // "New", "Existing", "Error"
    public string Message { get; set; }
}
