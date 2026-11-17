// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;

namespace Cotton.Server.Services
{
    public class BadHttpRequestExceptionHandler(ILogger<BadHttpRequestExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not BadHttpRequestException badRequest)
            {
                return false;
            }

            string? endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.DisplayName;
            logger.LogWarning(badRequest, "Rejected {Method} {Endpoint} with HTTP {StatusCode}. Request: {RequestId}",
                httpContext.Request.Method, endpoint, badRequest.StatusCode, httpContext.TraceIdentifier);
            httpContext.Response.StatusCode = badRequest.StatusCode;
            CottonResult result = new()
            {
                Success = false,
                StatusCode = (HttpStatusCode)badRequest.StatusCode,
                Message = ReasonPhrases.GetReasonPhrase(badRequest.StatusCode),
            };
            await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);
            return true;
        }
    }
}
