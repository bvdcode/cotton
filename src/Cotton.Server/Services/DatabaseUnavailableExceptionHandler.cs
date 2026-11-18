// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using System.Globalization;
using System.Net;

namespace Cotton.Server.Services
{
    public class DatabaseUnavailableExceptionHandler(ILogger<DatabaseUnavailableExceptionHandler> logger) : IExceptionHandler
    {
        private const int RetryAfterSeconds = 5;

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (!IsDatabaseUnavailable(exception))
            {
                return false;
            }

            string? endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.DisplayName;
            logger.LogWarning(exception, "Database is temporarily unavailable for {Method} {Endpoint}. Request: {RequestId}",
                httpContext.Request.Method, endpoint, httpContext.TraceIdentifier);
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            CottonResult result = new()
            {
                Success = false,
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Message = "Database is temporarily unavailable. Try again shortly.",
                MessageCode = "database_temporarily_unavailable",
            };
            await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);
            return true;
        }

        private static bool IsDatabaseUnavailable(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is PostgresException { SqlState: PostgresErrorCodes.CannotConnectNow })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
