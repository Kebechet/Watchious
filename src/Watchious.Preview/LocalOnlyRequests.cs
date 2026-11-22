using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Watchious.Preview;

// The preview and the inspector run an app and let their page click in it, so only this computer's own tools may use
// them. A browser lets any web page open a WebSocket to localhost, so a request from a page (one with an Origin) must
// come from this server's own page. DNS rebinding can point another site's name at this computer, so the Host must be
// this computer's name or an IP address. The watch app's own connection carries no Origin.
internal static class LocalOnlyRequests
{
    public static void UseLocalOnlyRequests(this WebApplication web)
    {
        web.Use(RejectOthersAsync);
    }

    internal static bool IsAllowed(HttpRequest request)
    {
        if (!IsThisComputer(request.Host.Host))
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var originUrl)
            && string.Equals(originUrl.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static Task RejectOthersAsync(HttpContext context, RequestDelegate next)
    {
        if (IsAllowed(context.Request))
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static bool IsThisComputer(string host)
    {
        var computerName = Dns.GetHostName();
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host.Trim('[', ']'), out _)
            || host.Equals(computerName, StringComparison.OrdinalIgnoreCase)
            || host.Equals(computerName + ".local", StringComparison.OrdinalIgnoreCase);
    }
}
