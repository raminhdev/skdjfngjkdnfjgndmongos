namespace Utilities.Services.Contracts
{
    public interface ISecurityService
    {
        void CheckFailureLoginAttemptAsync(string userName);
        void AddFailureLoginAttemptAsync(string userName);
    }
}
