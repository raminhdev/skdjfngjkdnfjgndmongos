namespace Utilities.Services.Contracts
{
    public interface IRandomService
    {
        string GetSecureAlphaNumericString(int len);
        string GetSecureNumericString(int len);
        string GetSecureRandomString(int len, string chars);
    }
}