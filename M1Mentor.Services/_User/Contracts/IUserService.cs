using M1Mentor.Services._User.DTOs.Results;
using M1Mentor.Services._User.DTOs.Updates;
using Microsoft.AspNetCore.Mvc;
using Utilities.MongoDatabase.Filter;

namespace M1Mentor.Services._User.Contracts
{
    public interface IUserService
    {
        Task<ActionResult> LoginAsync(UserLoginUpdate update);
        Task<ActionResult> RenewTokenAsync(UserRenewTokenUpdate update);
        Task<bool> LogoutAsync(string publicKey);

        Task<UserFilteredResult> CreateUserAsync(UserRegisterUpdate update);
        Task<UserFilteredResult> EditUserAsync(UserEditUpdate update);
        Task<bool> ChangePasswordAsync(UserResetPasswordUpdate update);
        Task<UserFilteredResult> ArchiveUserAsync(UserArchiveUserUpdate update);
        Task<UserFilteredResult> BanUserAsync(UserBanUpdate update);
        Task<string> DeleteUserByPublickeyAsync(UserDeleteUserUpdate update);
        Task<MonjoFilteredResult<UserFilteredForAdminResult>> GetAllUsersAsync(MonjoQuery query);
        Dictionary<string, Dictionary<string, string>> GetClassifiedPermissions();
        Task<bool> ResetPasswordAsync(UserManualChangePasswordUpdate update, string whoIs);
        Task<UserFilteredResult> GetUserByPublickeyAsync(string publickey);
        Task SyncPermissionsAsync();
    }
}