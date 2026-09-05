using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Utilities.Permissions
{
    public static class PermissionUtilities
    {
        //Don't forget to add primary permissions to this list (we use this list for remove primary permissions from list of permissions of users when we return response)
        private static readonly IEnumerable<string> primaryPermissions = ["Admin", "User"];

        /// <summary>
        /// get list of permissions value for return them in dictionary format (key: value of permission, value: description of permission)
        /// Correct permission value format:
        /// </summary>
        /// <param name="permissionsList">list of permissions value</param>
        /// <param name="exceptions">permissions that we ignore put them in result</param>
        /// <returns></returns>
        public static Dictionary<string, string> GeneratePermissionsWithDescription(IEnumerable<string> permissionsList, IEnumerable<string> exceptions = null)
        {
            var permissionsWithDescription = permissionsList
                .Distinct()
                .Where(permission => (exceptions == null || !exceptions.Contains(permission)) && !primaryPermissions.Contains(permission))
                .ToDictionary(
                    permission => permission,
                    permission => FormatPermissionDescription(permission)
                );

            return permissionsWithDescription;
        }

        /// <summary>
        /// Get static permissions of a primary Permission //get permissions that a primary Permission doesn't have
        /// Example: one of the endpoints (API) in the controller have "[Authorize(Permissions.GET_ALL_USERS, Permissions.CEO)]"
        /// This means "CEO" (primary permission (permission pack)) have "GET_ALL_USERS" by default and we don't need to give him "GET_ALL_USERS" permission. "GET_ALL_USERS" is his static permission
        /// So this method will return "GET_ALL_USERS" as static permission of "CEO"
        /// Attention: We should put secondary permission in first place of values of Authorize attribute because we get secondary permissions from first value of argument
        /// </summary>
        /// <param name="primaryPermission">One of the primary permissions value</param>
        /// <returns>
        /// Method will return secondary permissions that passed primary permission have them by default
        /// </returns>
        public static List<string> GetStaticPermissionsOfPrimaryPermission(string primaryPermission)
        {
            List<string> result = [];

            var apiControllerTypes = Assembly.GetEntryAssembly()
                .GetTypes()
                .Where(t => t.GetCustomAttributes<ApiControllerAttribute>().Any());

            foreach (var controllerType in apiControllerTypes)
            {
                // Get all methods in the controller
                var apis = controllerType.GetMethods();

                foreach (var apiInfo in apis)
                {
                    var authorizeAttribute = apiInfo.CustomAttributes.FirstOrDefault(q =>
                        q.AttributeType.FullName == "M1Mentor.Utilities.Filters.AuthorizeAttribute");

                    if (authorizeAttribute != null)
                    {
                        var attributeFirstArgument = authorizeAttribute.ConstructorArguments.FirstOrDefault();

                        if (attributeFirstArgument.Value != null)
                        {
                            var valuesOfFirstArgumentOfAttribute = GetValuesOfArgumentOfAttribute(attributeFirstArgument.Value);
                            if (valuesOfFirstArgumentOfAttribute == null || valuesOfFirstArgumentOfAttribute.Count <= 0)
                                continue;

                            if (valuesOfFirstArgumentOfAttribute.Contains(primaryPermission))
                                result.Add(valuesOfFirstArgumentOfAttribute[0]);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get other permissions of a primary Permission (permissions that primary permission doesn't have them)
        /// Example: one of the endpoints (API) in the controller have "[Authorize(Permissions.GET_ALL_USERS, Permissions.CEO)]"
        /// This means "CEO" (primary permission (permission pack)) have "GET_ALL_USERS" by default and we don't need to give him "GET_ALL_USERS" permission
        /// So this method won't return "GET_ALL_USERS" secondary permission, because this is static permission of "CEO"
        /// Attention: We should put secondary permission in first place of values of Authorize attribute because we get secondary permissions from first value of argument
        /// </summary>
        /// <param name="primaryPermission">One of the primary permissions value</param>
        /// <returns>
        /// Method will return secondary permissions that passed primary permission have them by default
        /// </returns>
        public static List<string> GetOtherPermissionsOfPrimaryPermission(string primaryPermission)
        {
            List<string> result = [];

            var apiControllerTypes = Assembly.GetEntryAssembly()
                .GetTypes()
                .Where(t => t.GetCustomAttributes<ApiControllerAttribute>().Any());

            foreach (var controllerType in apiControllerTypes)
            {
                // Get all methods in the controller
                var apis = controllerType.GetMethods();

                foreach (var apiInfo in apis)
                {
                    var authorizeAttribute = apiInfo.CustomAttributes.FirstOrDefault(q =>
                        q.AttributeType.FullName == "M1Mentor.Utilities.Filters.AuthorizeAttribute");

                    if (authorizeAttribute != null)
                    {
                        var attributeFirstArgument = authorizeAttribute.ConstructorArguments.FirstOrDefault();

                        if (attributeFirstArgument.Value != null)
                        {
                            var valuesOfFirstArgumentOfAttribute = GetValuesOfArgumentOfAttribute(attributeFirstArgument.Value);
                            if (valuesOfFirstArgumentOfAttribute == null || valuesOfFirstArgumentOfAttribute.Count <= 0)
                                continue;

                            if (!valuesOfFirstArgumentOfAttribute.Contains(primaryPermission))
                                result.Add(valuesOfFirstArgumentOfAttribute[0]);
                        }
                    }
                }
            }

            return result;
        }

        public static bool ContainsPrimaryPermission(string permission)
        {
            return primaryPermissions.Contains(permission);
        }

        #region Private Method

        private static string FormatPermissionDescription(string permission)
        {
            // Insert spaces before uppercase letters and apply replacements
            var content = string.Concat(permission.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : $"{c}"));

            return "Able to " + content.ToLower()
                          .Replace("all", "list of all")
                          .Replace("add", "add new")
                          .Replace("create", "create new");
        }

        private static List<string> GetValuesOfArgumentOfAttribute(object obj)
        {
            if (obj is ReadOnlyCollection<CustomAttributeTypedArgument> arguments)
            {
                return arguments.Select(argument => argument.Value?.ToString() ?? "null")?.ToList() ?? [];
            }

            return [];
        }

        #endregion
    }
}