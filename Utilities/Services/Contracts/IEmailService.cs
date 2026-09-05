namespace Utilities.Services.Contracts
{
    public interface IEmailService
    {
        Task SendVerificationCodeEmailAsync(string destinationEmail, string verificationCode);
        Task SendOneTimePasswordEmailAsync(string destinationEmail, string oneTimePassword);
    }
}
