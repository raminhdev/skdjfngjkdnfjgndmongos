namespace Utilities._Permissions.Utilities
{
    public static class PermissionUtilities
    {
        public static Dictionary<string, string> GetAllPermissions()
        {
            return Constants.Permissions.PermissionsList.ToDictionary(q => q.Title, q => q.Description);
        }

        public static Dictionary<string, Dictionary<string, string>> GetUserClassifiedPermissions()
        {
            IEnumerable<string> blackList = Constants.Permissions.AllRoles;

            Dictionary<string, Dictionary<string, string>> result = [];

            foreach (var category in Constants.Permissions.AllRoles)
                result[category] = Constants.Permissions.PermissionsList
                    .Where(p => p.Roles.Contains(category.ToLower()))
                    .Where(q => !blackList.Contains(q.Title)) // exclude roles permissions (we only give methods permission)
                    .ToDictionary(q => q.Title, q => q.Description);

            return result;
        }

        public static List<string> GetCodeOfPermissionsByTheirTitle(IEnumerable<string> permissionsTitle)
        {
            return [.. Constants.Permissions.PermissionsList.Where(p => permissionsTitle?.Contains(p.Title) ?? false).Select(p => p.Code)];
        }

        public static List<string> GetTitleOfPermissionsByTheirCode(IEnumerable<string> permissionsCode)
        {
            return [.. Constants.Permissions.PermissionsList.Where(p => permissionsCode?.Contains(p.Code) ?? false).Select(p => p.Title)];
        }
    }
}