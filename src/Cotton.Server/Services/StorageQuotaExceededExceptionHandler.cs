// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Diagnostics;

namespace Cotton.Server.Services;

/// <summary>
/// Converts logical storage quota failures into the WebDAV-compatible 507 response expected by sync clients.
/// </summary>
public sealed class StorageQuotaExceededExceptionHandler(
    ILogger<StorageQuotaExceededExceptionHandler> logger) : IExceptionHandler
{
    private const int InsufficientStorageStatusCode = 507;

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not StorageQuotaExceededException quotaExceeded)
        {
            return false;
        }

        logger.LogInformation(
            "Storage quota exceeded for request {Method} {Path}. Used: {UsedBytes} bytes, quota: {QuotaBytes} bytes, additional: {AdditionalBytes} bytes.",
            httpContext.Request.Method,
            httpContext.Request.Path,
            quotaExceeded.UsedBytes,
            quotaExceeded.QuotaBytes,
            quotaExceeded.AdditionalBytes);

        httpContext.Response.StatusCode = InsufficientStorageStatusCode;
        httpContext.Response.ContentType = "text/plain; charset=utf-8";
        await httpContext.Response.WriteAsync(quotaExceeded.Message, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
