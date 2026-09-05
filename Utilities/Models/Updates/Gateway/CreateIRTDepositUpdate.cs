using Utilities.Attributes;

namespace Utilities.Models.Updates.Gateway
{
    public class CreateIRTDepositUpdate
    {
        [NumericInputValidation(isRequired:true,mustBeNonZero: true)] public decimal Amount { get; set; }
        public string Description { get; set; }

    }
}
