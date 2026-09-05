using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Extensions;

namespace Utilities.Middlewares
{
    public class FirewallMiddleware(RequestDelegate next, FirewallSettings firewallSettings)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            foreach (var rule in firewallSettings.Rules)
            {
                if (Regex.Match(context.Request.Path, rule.Regex, RegexOptions.IgnoreCase).Success)
                {
                    var ipAddress = context.GetRequestIpv4();

                    rule.IPAddresses ??= [];

                    if (rule.IPAddresses.Contains(ipAddress) || rule.IPAddresses.Contains("*"))
                    {
                        if (rule.Policy == FirewallRulePolicy.Allow)
                        {
                            await next(context);
                            return;
                        }
                        else if (rule.Policy == FirewallRulePolicy.Deny)
                        {
                            await context.WriteToResponseAsync("Do not play around here", HttpStatusCode.Forbidden, ApiResultStatusCode.Forbidden);
                            return;
                        }
                    }
                }
            }

            await context.WriteToResponseAsync("GFY", HttpStatusCode.Forbidden, ApiResultStatusCode.Forbidden);
            return;
        }
    }
}
