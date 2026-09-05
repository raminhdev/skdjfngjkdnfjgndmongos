using M1Mentor.Domain.Collections;
using M1Mentor.Domain.Repositories.Contracts;
using M1Mentor.Services._User.Contracts;
using M1Mentor.Services._User.DTOs.Results;
using M1Mentor.Services._User.DTOs.Updates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities._Permissions.Constants;
using Utilities._Permissions.Utilities;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Exceptions;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.Models.Storages;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace M1Mentor.Services._User
{
    public class UserService(
        IUserRepository _userRepository,
        JwtServiceSettings _jwtSettings,
        IJwtService _jwtService,
        IPasswordService _passwordService,
        ICaptchaService _captchaService,
        SecurityStampStorage _securityStampStorage,
        ILogger<UserService> _logger)
        : IUserService, IScopedDependency
    {
        public async Task<ActionResult> LoginAsync(UserLoginUpdate update)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);

            //await _captchaService.ValidateCaptchaAsync(update.CaptchaCode);

            var user = await _userRepository.AsQueryable()
                .FirstOrDefaultAsync(q => q.UserName == update.UserName);

            if (user == null || !_passwordService.Verify(update.Password, user.PasswordHash))
                throw new BadRequestException("Invalid username or password.");
            if (user.State == UserState.Archived)
                throw new BadRequestException("An archived user cannot log in.");
            if (user.State == UserState.Ban)
                throw new BadRequestException("Access denied. User is banned.");

            user = AddLoginDateToUser(user);
            await _userRepository.ReplaceOneAsync(user);

            var tokenResult = _jwtService.Generate(BuildClaims(user));

            var refreshTokenHash = _passwordService.Hash(tokenResult.refresh_token);
            var expiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiresAfterDays);
            await UpdateRefreshTokenAsync(user.PublicKey, refreshTokenHash, expiry);

            return new JsonResult(tokenResult);
        }

        public async Task<ActionResult> RenewTokenAsync(UserRenewTokenUpdate update)
        {
            _logger.LogInformation(
                "RenewToken request: grant_type={GrantType}, accessTokenPresent={AccessPresent}, " +
                "refreshTokenPresent={RefreshPresent}, clientIdPresent={ClientIdPresent}, clientSecretPresent={ClientSecretPresent}",
                update.grant_type,
                !string.IsNullOrWhiteSpace(update.access_token),
                !string.IsNullOrWhiteSpace(update.refresh_token),
                !string.IsNullOrWhiteSpace(update.client_id),
                !string.IsNullOrWhiteSpace(update.client_secret));


            if (string.IsNullOrWhiteSpace(update.access_token))
                throw new BadRequestException(
                    "Access token is required to identify the account for renewal. Please log in again.");

            System.Security.Claims.ClaimsPrincipal principal;
            try
            {
                principal = _jwtService.GetPrincipalFromExpiredToken(update.access_token);
            }
            catch
            {
                throw new BadRequestException("Invalid access token.");
            }

            var publicKey = principal.Claims
                                .FirstOrDefault(c => c.Type == Claims.PublicKey.ToDisplay())?.Value ??
                            throw new BadRequestException("Invalid token claims.");

            var user = await _userRepository.GetUserByPublicKeyAsync(publicKey);

            if (user.State == UserState.Archived)
                throw new BadRequestException("An archived user cannot log in.");
            if (user.State == UserState.Ban)
                throw new BadRequestException("Access denied. User is banned.");

            if (string.IsNullOrEmpty(user.RefreshTokenHash) ||
                user.RefreshTokenExpiresAt == null ||
                user.RefreshTokenExpiresAt < DateTime.UtcNow)
                throw new AuthorizationException("Refresh token has expired. Please log in again.");

            if (!_passwordService.Verify(update.refresh_token, user.RefreshTokenHash))
                throw new AuthorizationException("Invalid refresh token.");


            var newTokenResult = _jwtService.Generate(BuildClaims(user));
            newTokenResult.refresh_token = update.refresh_token;

            return new JsonResult(newTokenResult);
        }

        public async Task<bool> LogoutAsync(string publicKey)
        {
            var newSecurityStamp = Guid.NewGuid().ToString("N");

            var filter = Builders<User>.Filter.Eq(u => u.PublicKey, publicKey);
            var updateDef = Builders<User>.Update
                .Set(u => u.RefreshTokenHash, null)
                .Set(u => u.RefreshTokenExpiresAt, null)
                .Set(u => u.SecurityStamp, newSecurityStamp);

            var user = await _userRepository.FindOneAndUpdateAsync(filter, updateDef, CancellationToken.None);

            if (user == null)
                throw new NotFoundException("User not found.");

            _securityStampStorage.UpdateSecurityStamp(UserType.User.ToDisplay(), publicKey, newSecurityStamp);

            return true;
        }

        public async Task<UserFilteredResult> CreateUserAsync(UserRegisterUpdate update)
        {
            if (await _userRepository.AsQueryable().AnyAsync(q => q.UserName == update.UserName))
                throw new BadRequestException("Username was already taken");

            var user = new User
            {
                UserName = update.UserName,
                State = UserState.Active,
                PasswordHash = _passwordService.Hash(update.Password),
                Permissions =
                    PermissionUtilities.GetCodeOfPermissionsByTheirTitle(
                        [.. update.Permissions, update.Role.ToString()]),
                EmailAddress = update.EmailAddress,
                FullName = update.FullName,
                NickName = update.NickName,
                Role = update.Role,
                RoleDescription = update.RoleDescription,
                PhoneNumber = update.PhoneNumber,
            };

            await _userRepository.InsertOneAsync(user);

            return MapToUserFilteredResult(user);
        }

        public async Task<UserFilteredResult> EditUserAsync(UserEditUpdate update)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(update.PublicKey);
            var oldPermissions = user.Permissions.ToList();

            if (await _userRepository.AsQueryable()
                    .AnyAsync(q => q.UserName == update.UserName && q.PublicKey != update.PublicKey))
                throw new BadRequestException("Username was already taken");

            user.UserName = update.UserName;
            user.State = update.State;
            user.FullName = update.FullName;
            user.NickName = update.NickName;
            user.Role = update.Role;
            user.RoleDescription = update.RoleDescription;
            user.PhoneNumber = !string.IsNullOrEmpty(update.PhoneNumber) ? update.PhoneNumber : null;
            user.EmailAddress = !string.IsNullOrEmpty(update.PhoneNumber) ? update.EmailAddress : null;
            user.Permissions = update.Permissions != null && update.Permissions.Count != 0
                ? PermissionUtilities.GetCodeOfPermissionsByTheirTitle([.. update.Permissions, user.Role.ToString()])
                : user.Permissions;

            if (oldPermissions.Count != user.Permissions.Count ||
                !oldPermissions.All(p => user.Permissions.Contains(p)))
                user.SecurityStamp = Guid.NewGuid().ToString("N");

            await _userRepository.ReplaceOneAsync(user);

            return MapToUserFilteredResult(user);
        }

        public async Task<bool> ChangePasswordAsync(UserResetPasswordUpdate update)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(update.PublicKey);
            user.PasswordHash = _passwordService.Hash(update.Password);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;

            await _userRepository.ReplaceOneAsync(user);

            _securityStampStorage.UpdateSecurityStamp(UserType.User.ToDisplay(), user.PublicKey, user.SecurityStamp);

            return true;
        }

        public async Task<UserFilteredResult> ArchiveUserAsync(UserArchiveUserUpdate update)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(update.UserPublicKey);

            user.State = update.ShouldArchive
                ? UserState.Archived
                : UserState.Active;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;

            var updateDef = Builders<User>.Update
                .Set(u => u.State, user.State)
                .Set(u => u.SecurityStamp, user.SecurityStamp)
                .Set(u => u.RefreshTokenHash, user.RefreshTokenHash)
                .Set(u => u.RefreshTokenExpiresAt, user.RefreshTokenExpiresAt);

            await _userRepository.FindOneAndUpdateAsync(q => q.PublicKey == update.UserPublicKey, updateDef);

            _securityStampStorage.UpdateSecurityStamp(UserType.User.ToDisplay(), update.UserPublicKey,
                user.SecurityStamp);

            return MapToUserFilteredResult(user);
        }

        public async Task<UserFilteredResult> BanUserAsync(UserBanUpdate update)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(update.PublicKey);
            user.State = update.MakeBan ? UserState.Ban : UserState.Active;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;

            var updateDef = Builders<User>.Update
                .Set(u => u.State, user.State)
                .Set(u => u.SecurityStamp, user.SecurityStamp)
                .Set(u => u.RefreshTokenHash, user.RefreshTokenHash)
                .Set(u => u.RefreshTokenExpiresAt, user.RefreshTokenExpiresAt);

            await _userRepository.FindOneAndUpdateAsync(q => q.PublicKey == update.PublicKey, updateDef);

            _securityStampStorage.UpdateSecurityStamp(UserType.User.ToDisplay(), update.PublicKey, user.SecurityStamp);

            return MapToUserFilteredResult(user);
        }

        public async Task<string> DeleteUserByPublickeyAsync(UserDeleteUserUpdate update)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(update.PublicKey);

            await _userRepository.DeleteOneAsync(q => q.PublicKey == update.PublicKey);

            return user.PublicKey;
        }

        public async Task<MonjoFilteredResult<UserFilteredForAdminResult>> GetAllUsersAsync(MonjoQuery query)
        {
            query.WithBase<UserFilteredForAdminResult>();
            var data = await _userRepository.AsQueryable()
                .Apply(query.Where)
                .Apply(query.Order)
                .Select(user => new UserFilteredForAdminResult
                {
                    CreatedByInfo = user.CreatedByInfo,
                    CreatedMoment = user.CreatedMoment,
                    ModifiedByInfo = user.ModifiedByInfo,
                    ModifiedMoment = user.ModifiedMoment,
                    State = user.State,
                    PublicKey = user.PublicKey,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    NickName = user.NickName,
                    Role = user.Role,
                    RoleDescription = user.RoleDescription,
                    Permissions = user.Permissions,
                    PhoneNumber = user.PhoneNumber,
                    EmailAddress = user.EmailAddress,
                    LoginDates = user.LoginDates,
                })
                .ExecuteAsync(query);

            data.Data =
            [
                .. data.Data.Select(user =>
                {
                    user.Permissions = PermissionUtilities.GetTitleOfPermissionsByTheirCode(user.Permissions);
                    return user;
                })
            ];

            return data;
        }

        public Dictionary<string, Dictionary<string, string>> GetClassifiedPermissions()
            => PermissionUtilities.GetUserClassifiedPermissions();

        public async Task<bool> ResetPasswordAsync(UserManualChangePasswordUpdate update, string whoIs)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(whoIs);

            if (user == null || !_passwordService.Verify(update.OldPassword, user.PasswordHash))
                throw new BadRequestException("User not found.");
            if (user.State == UserState.Ban)
                throw new BadRequestException("User is banned.");
            if (_passwordService.Verify(update.NewPassword, user.PasswordHash))
                throw new BadRequestException("New password must be different from old password.");

            user.PasswordHash = _passwordService.Hash(update.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            await _userRepository.ReplaceOneAsync(user);

            return true;
        }

        public async Task<UserFilteredResult> GetUserByPublickeyAsync(string publickey)
        {
            var user = await _userRepository.GetUserByPublicKeyAsync(publickey);
            return MapToUserFilteredResult(user);
        }

        #region Scheduler Methods

        public async Task SyncPermissionsAsync()
        {
            foreach (var role in Permissions.AllRoles)
            {
                var newPermissionsForRole = Permissions.PermissionsList
                    .Where(p => p.Roles.Contains(role.ToLower()) && p.IsNew)
                    .Select(p => new { p.Code, p.Title })
                    .ToList();
                var newPermissionsCodeForRole = newPermissionsForRole.Select(q => q.Code);
                var newPermissionsTitleForRole = newPermissionsForRole.Select(q => q.Title);

                if (newPermissionsForRole.Count == 0)
                    continue;

                UserRole parsedRole = Enum.Parse<UserRole>(role, true);
                var roleFilter = Builders<User>.Filter.Eq(u => u.Role, parsedRole);

                var missingPermissionsFilter = Builders<User>.Filter.Not(
                    Builders<User>.Filter.All(u => u.Permissions, newPermissionsCodeForRole)
                );

                var finalFilter = Builders<User>.Filter.And(roleFilter, missingPermissionsFilter);

                var update = Builders<User>.Update.AddToSetEach(
                    u => u.Permissions,
                    newPermissionsCodeForRole
                );

                var result = await _userRepository.UpdateManyAsync(finalFilter, update, CancellationToken.None);

                _logger.LogInformation("Updated {Count} users for role {Role} with permissions: {Permissions}",
                    result.ModifiedCount, role, string.Join(", ", newPermissionsTitleForRole));
            }
        }

        #endregion

        #region Private Methods

        private void ValidateClientInfo(string clientId, string clientSecret)
        {
            if (!clientId.HasValue() ||
                !clientSecret.HasValue() ||
                !_jwtSettings.ClientInfo.ContainsKey(clientId.ToLower()) ||
                !_jwtSettings.ClientInfo[clientId.ToLower()].Equals(clientSecret, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException(ApiResultStatusCode.OAuth.ToDisplay());
        }

        private static User AddLoginDateToUser(User user)
        {
            try
            {
                if (user.LoginDates is { Count: >= 1 })
                {
                    user.LoginDates.Add(DateTime.UtcNow);

                    var userLoginDates = user.LoginDates.OrderByDescending(x => x).ToList();
                    if (userLoginDates.Count <= 20) return user;
                    var twentiethDate = userLoginDates[19];
                    userLoginDates.RemoveAll(q => q <= twentiethDate);
                    user.LoginDates = userLoginDates;
                }
                else
                    user.LoginDates = [DateTime.UtcNow];

                return user;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        private static IEnumerable<System.Security.Claims.Claim> BuildClaims(User user)
        {
            var claims = new List<System.Security.Claims.Claim>
            {
                new(Claims.PublicKey.ToDisplay(), user.PublicKey),
                new(Claims.UserType.ToDisplay(), UserType.User.ToDisplay()),
                new(Claims.SecurityStamp.ToDisplay(), user.SecurityStamp),
                new(Claims.Role.ToDisplay(), user.Role.ToString()),
                new(Claims.FullName.ToDisplay(), user.FullName),
            };

            if (user.Permissions != null)
                claims.AddRange(user.Permissions.Select(p =>
                    new System.Security.Claims.Claim(Claims.Permission.ToDisplay(), p)));

            return claims;
        }

        private static UserFilteredResult MapToUserFilteredResult(User user)
        {
            return new UserFilteredResult
            {
                CreatedByInfo = user.CreatedByInfo,
                CreatedMoment = user.CreatedMoment,
                ModifiedByInfo = user.ModifiedByInfo,
                ModifiedMoment = user.ModifiedMoment,
                State = user.State,
                PublicKey = user.PublicKey,
                UserName = user.UserName,
                FullName = user.FullName,
                NickName = user.NickName,
                Role = user.Role,
                RoleDescription = user.RoleDescription,
                Permissions = PermissionUtilities.GetTitleOfPermissionsByTheirCode(user.Permissions),
                PhoneNumber = user.PhoneNumber,
                EmailAddress = user.EmailAddress,
                LoginDates = user.LoginDates
            };
        }

        private async Task UpdateRefreshTokenAsync(string publicKey, string refreshTokenHash, DateTime expiresAt)
        {
            var filter = Builders<User>.Filter
                .Eq(u => u.PublicKey, publicKey);

            var update = Builders<User>.Update
                .Set(u => u.RefreshTokenHash, refreshTokenHash)
                .Set(u => u.RefreshTokenExpiresAt, expiresAt);

            var result = await _userRepository.FindOneAndUpdateAsync(filter, update, CancellationToken.None);
        }

        #endregion
    }
}