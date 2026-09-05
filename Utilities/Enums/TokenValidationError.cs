namespace Utilities.Enums
{
    /// <summary>
    /// Why an access token failed validation. Used to surface a precise, honest error message to the
    /// caller instead of a generic "unauthorized".
    /// </summary>
    public enum TokenValidationError
    {
        None = 0,
        Missing,
        Expired,
        InvalidSignature,
        InvalidIssuer,
        InvalidAudience,
        Invalid
    }
}
