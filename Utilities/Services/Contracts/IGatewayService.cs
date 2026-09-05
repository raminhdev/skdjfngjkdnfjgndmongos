using Utilities.Models.Results;
using Utilities.Models.Updates.Gateway;

namespace Utilities.Services.Contracts
{
    public interface IGatewayService
    {
        Task<string> CreateZarinPalDepositAsync(string orderReference, CreateIRTDepositUpdate update);
        Task<VerifyIRTDepositResult> VerifyZarinPalDepositAsync(string trackId, decimal amount);
    }
}