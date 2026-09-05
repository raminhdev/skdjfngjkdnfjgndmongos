using System.Text.Json;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.Models.Results;
using Utilities.Models.Settings;
using Utilities.Models.Updates.Gateway;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services;

public class GatewayService(IRTHandlerSettings _irtHandlerSettings) : IGatewayService, IScopedDependency
{
    private readonly HttpClient httpClient = new();

    public async Task<string> CreateZarinPalDepositAsync(string orderReference, CreateIRTDepositUpdate update)
    {
        var content =
            new
            {
                merchant_id = _irtHandlerSettings.ZarinPalMerchantId,
                amount = (update.Amount * 10),
                callback_url = _irtHandlerSettings.WebCallbackUrl,
                description = update.Description,
                metadata = new { order_id = orderReference },
            };

        var result = await httpClient.PostAsJsonAsync<JsonElement>($"https://api.zarinpal.com/pg/v4/payment/request.json", content);
        try
        {
            if (result.GetProperty("data").GetProperty("code").GetInt32() != 100)
                throw new Exception($"Error code: {result.GetProperty("data").GetProperty("code").GetInt32()}");

            var track = result.GetProperty("data").GetProperty("authority").GetString();
            return track;
            //return "https://www.zarinpal.com/pg/StartPay/" + cod;
        }
        catch (Exception e)
        {
            throw new BadRequestException("مبلغ پرداختی نباید بیشتر از 100 میلیون تومان باشد");
        }

    }

    public async Task<VerifyIRTDepositResult> VerifyZarinPalDepositAsync(string trackId, decimal amount)
    {
        var content = new
        {
            merchant_id = _irtHandlerSettings.ZarinPalMerchantId,
            authority = trackId,
            amount = amount * 10
        };

        var result = await httpClient.PostAsJsonAsync<JsonElement>($"https://api.zarinpal.com/pg/v4/payment/verify.json", content);
        //If code is 101 it means this was verified once
        if (result.GetProperty("data").GetProperty("code").GetInt32() != 100 || result.GetProperty("data").GetProperty("message").GetString() != "Verified")
            throw new BadRequestException("پرداخت تایید نشده است");


        return new VerifyIRTDepositResult
        {
            OrderId = result.GetProperty("data").GetProperty("order_id").GetString(),
            //DepositId = result.GetProperty("data").GetProperty("ref_id").GetInt32().ToString(),
            Reference = result.GetProperty("data").GetProperty("ref_id").GetInt64().ToString()
        };
    }


}
