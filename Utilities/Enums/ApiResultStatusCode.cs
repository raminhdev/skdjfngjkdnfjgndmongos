using System.ComponentModel.DataAnnotations;

namespace Utilities.Enums
{
    public enum ApiResultStatusCode
    {
        [Display(Name = "success")]
        Success = 0,

        [Display(Name = "try again 5 minutes later")]
        ServerError = 1,

        [Display(Name = "Params are not valid")]
        BadRequest = 2,

        [Display(Name = "not found")]
        NotFound = 3,



        [Display(Name = "Empty List")]
        ListEmpty = 4,

        [Display(Name = "There is a problem in process try again 5 minutes later")]
        LogicError = 5,

        [Display(Name = "Authorize Error")]
        UnAuthorized = 6,

        [Display(Name = "Access Denied")]
        Forbidden = 7,

        [Display(Name = "Role Not Found")]
        RoleNotFound = 8,

        [Display(Name = "Duplicated request")]
        Duplicated = 9,

        [Display(Name = "Length is too mutch")]
        Length = 10,

        [Display(Name = "OAuth data is wrong!")]
        OAuth = 11,

        [Display(Name = "user is Ban")]
        Ban = 12,

        [Display(Name = "InvalidCaptcha")]
        InvalidCaptcha = 13,

        [Display(Name = "field is required")]
        Required = 14,

        [Display(Name = "Too many requests. try again later")]
        TooManyRequests = 15,

        [Display(Name = "nonce not found")]
        NonceNotFound = 16,

        [Display(Name = "Insufficient Balance!")]
        InsufficientBalance = 17,

        [Display(Name ="Conflict")]
        Conflict = 18,
        
        [Display(Name = "AccessToken Expired")]
        AccessTokenExpired = 19,
        
        [Display(Name = "RefreshToken Expired")]
        RefreshTokenExpired = 20,
        
        [Display(Name = "Log Out")]
        LogOut = 21,
    }
}
