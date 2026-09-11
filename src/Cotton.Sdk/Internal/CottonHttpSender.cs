// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace Cotton.Sdk.Internal
{
    internal class CottonHttpSender(HttpClient _httpClient, ILogger<CottonHttpTransport> _logger)
    {
        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            HttpMethod method,
            string path,
            CancellationToken cancellationToken)
        {
            string redactedPath = CottonHttpResponseReader.RedactPath(path);
            long started = Stopwatch.GetTimestamp();
            _logger.LogDebug("Sending Cotton API request {Method} {Path}.", method.Method, redactedPath);
            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
                LogCompletedRequest(method, redactedPath, response.StatusCode, elapsed);
                return response;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Cotton API request {Method} {Path} failed before receiving a response.",
                    method.Method,
                    redactedPath);
                throw;
            }
        }

        private void LogCompletedRequest(
            HttpMethod method,
            string redactedPath,
            HttpStatusCode statusCode,
            TimeSpan elapsed)
        {
            if ((int)statusCode >= 400)
            {
                _logger.LogWarning(
                    "Cotton API request {Method} {Path} completed with status {StatusCode} in {ElapsedMilliseconds} ms.",
                    method.Method,
                    redactedPath,
                    (int)statusCode,
                    elapsed.TotalMilliseconds);
                return;
            }

            _logger.LogDebug(
                "Cotton API request {Method} {Path} completed with status {StatusCode} in {ElapsedMilliseconds} ms.",
                method.Method,
                redactedPath,
                (int)statusCode,
                elapsed.TotalMilliseconds);
        }
    }
}
