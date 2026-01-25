namespace IdentityService.Domain.Constants;

public static class Permissions
{
    public static class Users
    {
        public const string View = "UserManagement.Users.View";
        public const string Create = "UserManagement.Users.Create";
        public const string Edit = "UserManagement.Users.Edit";
        public const string Delete = "UserManagement.Users.Delete";
    }

    public static class Roles
    {
        public const string View = "UserManagement.Roles.View";
        public const string Create = "UserManagement.Roles.Create";
        public const string Edit = "UserManagement.Roles.Edit";
        public const string Delete = "UserManagement.Roles.Delete";
    }
    
    public static class PermissionsManagement
    {
        public const string View = "UserManagement.Permissions.View";
        public const string Assign = "UserManagement.Permissions.Assign";
    }
}
