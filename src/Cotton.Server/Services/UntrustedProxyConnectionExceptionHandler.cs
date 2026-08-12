// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Cotton.Server.Services
{
    public class UntrustedProxyConnectionExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not UntrustedProxyConnectionException)
            {
                return false;
            }

            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            CottonResult result = CottonResult
                .Forbidden("The request did not arrive through the configured trusted reverse proxy.")
                .WithMessageCode("untrusted_proxy_connection");
            await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);
            return true;
        }
    }
}
