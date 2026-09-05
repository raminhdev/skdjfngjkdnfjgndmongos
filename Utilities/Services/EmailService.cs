using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    internal class EmailService(EmailSettings _emailSettings) : IEmailService, IScopedDependency
    {
        public async Task SendVerificationCodeEmailAsync(string destinationEmail, string verificationCode)
        {
            try
            {
                var emailBody = new TextPart(TextFormat.Html)
                {
                    Text = $"<body style=\"font-family: Arial, sans-serif; background-color: #f4f4f4; color: #333; margin: 0; padding: 0;\">\r\n\r\n<div style=\"max-width: 600px; margin: 20px auto; background-color: #fff; border-radius: 8px; padding: 20px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);\">\r\n    <h1 style=\"color: #333; font-size: 24px; margin-bottom: 10px;\">{verificationCode}</h1>\r\n    <p style=\"line-height: 1.6; margin-bottom: 20px;\">Hi [Recipient's First Name],</p>\r\n    <p style=\"line-height: 1.6; margin-bottom: 20px;\">Thank you for signing up with [Your Company Name]! To complete your registration, please verify your email address by clicking the button below:</p>\r\n    <p style=\"text-align: center; margin-bottom: 20px;\">\r\n        <a href=\"#\" style=\"display: inline-block; background-color: #007bff; color: #fff; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-size: 16px;\">Verify My Email</a>\r\n    </p>\r\n    <p style=\"line-height: 1.6; margin-bottom: 20px;\">If you did not create an account with us, please disregard this email.</p>\r\n    <div style=\"margin-top: 20px; font-size: 12px; color: #777; text-align: center;\">\r\n        <p>Thanks for helping us keep your account secure!</p>\r\n        <p>[Your Company Name] | [Contact Information]</p>\r\n    </div>\r\n</div>\r\n\r\n</body>"
                };

                await SendEmailAsync(destinationEmail, "Email Verification Code", emailBody);
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        public async Task SendOneTimePasswordEmailAsync(string destinationEmail, string oneTimePassword)
        {
            var emailBody = new TextPart(TextFormat.Html)
            {
                Text = $"<body style=\"font-family: Arial, sans-serif; background-color: #f4f4f4; color: #333; margin: 0; padding: 0;\">\r\n\r\n<div style=\"max-width: 600px; margin: 20px auto; background-color: #fff; border-radius: 8px; padding: 20px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);\">\r\n    <h1 style=\"color: #333; font-size: 24px; margin-bottom: 10px;\">{oneTimePassword}</h1>\r\n    <p style=\"line-height: 1.6; margin-bottom: 20px;\">Hi</p>\r\n    <p style=\"line-height: 1.6; margin-bottom: 20px;\">Thank you for signing up with [Your Company Name]! To complete your registration, please verify your email address by clicking the button below:</p>\r\n    <p style=\"text-align: center; margin-bottom: 20px;\">\r\n        <a href=\"#\" style=\"display: inline-block; background-color: #007bff; color: #fff; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-size: 16px;\">Verify My Email</a>\r\n    </p>\r\n    <p style=\"line-height: 1.6; margin-bottom: 20px;\">If you did not create an account with us, please disregard this email.</p>\r\n    <div style=\"margin-top: 20px; font-size: 12px; color: #777; text-align: center;\">\r\n        <p>Thanks for helping us keep your account secure!</p>\r\n    </div>\r\n</div>\r\n\r\n</body>"
            };

            await SendEmailAsync(destinationEmail, "One Time Password", emailBody);
        }

        #region Private Methods

        private async Task SendEmailAsync(string destinationEmail, string subject, TextPart body)
        {
            var email = new MimeMessage();
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_emailSettings.EmailHost, 465, SecureSocketOptions.SslOnConnect);
            await smtp.AuthenticateAsync(_emailSettings.EmailUserName, _emailSettings.EmailPassword);


            email.From.Add(MailboxAddress.Parse(_emailSettings.EmailUserName));
            email.Subject = subject;
            email.Body = body;
            email.To.Add(MailboxAddress.Parse(destinationEmail));


            smtp.Timeout = 1000;
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        #endregion
    }
}
