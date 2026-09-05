namespace Utilities.Services.Contracts
{
    public interface ISmsService
    {
        //Task SendTextMessageAsync(string mobileNumber, string message);
        Task SendVerificationMessageAsync(string mobileNumber, string code);
        Task SendMessageAsync(string mobileNumber, string templateName, params string[] data);

        Task SendOneTimePasswordAsync(string mobileNumber, string password);
        Task SendPlanCompletedMessage(string mobileNumber);
        Task SendPlanRequestMessage();
        Task SendTicketNotificationMessage();

    }
}