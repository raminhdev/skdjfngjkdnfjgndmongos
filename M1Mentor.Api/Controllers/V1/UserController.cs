using Asp.Versioning;
using M1Mentor.Services._User.Contracts;
using M1Mentor.Services._User.DTOs.Results;
using M1Mentor.Services._User.DTOs.Updates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Utilities._Permissions.Constants;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Utilities.MongoDatabase.Filter;

namespace M1Mentor.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserController(IUserService _userService) : ApiBaseController
    {
        [HttpPost("[action]")]
        [CustomRateLimit(message: "Too many login attempts. Please try again later", periodSeconds: (5 * 60),
            maxAttemptsCount: 10, lockoutDurationMinutes: 5)]
        [SwaggerOperation(Tags = ["Auth"])]
        public async Task<ActionResult> Login(UserLoginUpdate update)
            => await _userService.LoginAsync(update);

        [HttpPost("[action]")]
        [AllowAnonymous]
        [SwaggerOperation(Tags = ["Auth"])]
        [CustomRateLimit(
            message: "Too many requests. Please try again later.",
            periodSeconds: 5 * 60,
            maxAttemptsCount: 10,
            lockoutDurationMinutes: 5)]
        public async Task<ActionResult> RenewTokenAsync([FromBody] UserRenewTokenUpdate update)
            => await _userService.RenewTokenAsync(update);

        [HttpPost("[action]")]
        [global::Utilities.Filters.Authorize]
        [SwaggerOperation(Tags = ["Auth"])]
        public async Task<bool> LogoutAsync()
            => await _userService.LogoutAsync(PublicKey);


        [HttpPost("[action]")]
        // [Authorize(Permissions.CreateUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<UserFilteredResult> CreateAsync(UserRegisterUpdate update)
            => await _userService.CreateUserAsync(update);

        [HttpPut("[action]")]
        [global::Utilities.Filters.Authorize(Permissions.EditUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<bool> ChangePasswordAsync(UserResetPasswordUpdate update)
            => await _userService.ChangePasswordAsync(update);

        [HttpPut("[action]")]
        [global::Utilities.Filters.Authorize(Permissions.EditUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<UserFilteredResult> EditAsync(UserEditUpdate update)
            => await _userService.EditUserAsync(update);

        [HttpPut("[action]")]
        [global::Utilities.Filters.Authorize(Permissions.ArchiveUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<UserFilteredResult> ArchiveAsync(UserArchiveUserUpdate update)
            => await _userService.ArchiveUserAsync(update);

        [HttpPut("[action]")]
        [global::Utilities.Filters.Authorize(Permissions.BanUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<UserFilteredResult> BanAsync(UserBanUpdate update)
            => await _userService.BanUserAsync(update);

        [HttpDelete("[action]")]
        [global::Utilities.Filters.Authorize(Permissions.DeleteUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<string> DeleteAsync(UserDeleteUserUpdate update)
            => await _userService.DeleteUserByPublickeyAsync(update);

        [HttpPost("[action]")]
        [global::Utilities.Filters.Authorize(Permissions.GetAllUsers)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public async Task<MonjoFilteredResult<UserFilteredForAdminResult>> GetAllAsync(MonjoQuery query)
            => await _userService.GetAllUsersAsync(query);

        //[HttpGet("[action]")]
        //[Authorize(Permissions.GetAllUsers)]
        //[SwaggerOperation(Tags = ["UserAdmin"])]
        //public async Task<Dictionary<string, int>> GetCountOfEachRoleAsync()
        //    => await _userService.GetCountOfEachRoleAsync(PublicKey);

        [HttpGet("[action]")]
        // [Authorize(Permissions.CreateUser, Permissions.EditUser)]
        [SwaggerOperation(Tags = ["UserAdmin"])]
        public Dictionary<string, Dictionary<string, string>> GetClassifiedPermissions()
            => _userService.GetClassifiedPermissions();


        [HttpPut("[action]")]
        [global::Utilities.Filters.Authorize]
        [SwaggerOperation(Tags = ["User"])]
        public async Task<bool> ResetPasswordAsync(UserManualChangePasswordUpdate update)
            => await _userService.ResetPasswordAsync(update, PublicKey);

        [HttpGet("[action]")]
        [global::Utilities.Filters.Authorize]
        [SwaggerOperation(Tags = ["User"])]
        public async Task<UserFilteredResult> GetAsync()
            => await _userService.GetUserByPublickeyAsync(PublicKey);
    }
}