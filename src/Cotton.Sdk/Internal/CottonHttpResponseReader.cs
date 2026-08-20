// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Cotton.Sdk.Internal
{
    internal static class CottonHttpResponseReader
    {
        private const int ResponsePreviewLength = 180;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static async Task EnsureExpectedStatusAsync(
            HttpResponseMessage response,
            HttpMethod method,
            string path,
            HttpStatusCode expectedStatusCode,
            CancellationToken cancellationToken)
        {
            if (response.StatusCode == expectedStatusCode)
            {
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccessAsync(response, method, path, cancellationToken).ConfigureAwait(false);
                return;
            }

            throw new CottonApiException(
                response.StatusCode,
                null,
                $"Cotton API request {FormatRequestLabel(method, path)} returned unexpected status " +
                $"{(int)response.StatusCode} ({response.StatusCode}); expected " +
                $"{(int)expectedStatusCode} ({expectedStatusCode}).");
        }

        public static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            HttpMethod? method,
            string? path,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string? body = response.Content is null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string requestLabel = FormatRequestLabel(method, path);
            string message = requestLabel.Length == 0
                ? $"Cotton API request failed with status {(int)response.StatusCode} ({response.StatusCode})."
                : $"Cotton API request {requestLabel} failed with status " +
                  $"{(int)response.StatusCode} ({response.StatusCode}).";
            if (!string.IsNullOrWhiteSpace(body))
            {
                message += " Response: " + CreateResponsePreview(body);
            }

            throw new CottonApiException(response.StatusCode, body, message);
        }

        public static async Task<T> ReadRequiredJsonAsync<T>(
            HttpResponseMessage response,
            HttpMethod method,
            string path,
            CancellationToken cancellationToken,
            bool ensureSuccess)
        {
            if (ensureSuccess)
            {
                await EnsureSuccessAsync(response, method, path, cancellationToken).ConfigureAwait(false);
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                throw CreateEmptyJsonException(response, method, path);
            }

            T? result;
            try
            {
                result = JsonSerializer.Deserialize<T>(body, JsonOptions);
            }
            catch (JsonException exception)
            {
                string contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
                throw new CottonApiException(
                    response.StatusCode,
                    body,
                    $"Cotton API request {FormatRequestLabel(method, path)} returned invalid JSON " +
                    $"with content type '{contentType}' and status " +
                    $"{(int)response.StatusCode} ({response.StatusCode}). Response: " +
                    CreateResponsePreview(body),
                    exception);
            }

            return result ?? throw CreateEmptyJsonException(response, method, path);
        }

        public static int ReadRequiredTotalCount(
            HttpResponseMessage response,
            HttpMethod method,
            string path)
        {
            const string headerName = "X-Total-Count";
            if (!response.Headers.TryGetValues(headerName, out IEnumerable<string>? headerValues))
            {
                throw new CottonApiException(
                    response.StatusCode,
                    null,
                    $"Cotton API request {FormatRequestLabel(method, path)} did not include the " +
                    $"required {headerName} response header.");
            }

            string[] values = [.. headerValues];
            if (values.Length != 1
                || !int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out int totalCount))
            {
                throw new CottonApiException(
                    response.StatusCode,
                    null,
                    $"Cotton API request {FormatRequestLabel(method, path)} returned an invalid " +
                    $"{headerName} response header.");
            }

            return totalCount;
        }

        public static string RedactPath(string path)
        {
            int queryIndex = path.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex < 0 || queryIndex == path.Length - 1)
            {
                return path;
            }

            string route = path[..queryIndex];
            string[] parts = path[(queryIndex + 1)..].Split('&');
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                int equalsIndex = part.IndexOf('=', StringComparison.Ordinal);
                string key = equalsIndex < 0 ? part : part[..equalsIndex];
                if (IsSensitiveQueryKey(key))
                {
                    parts[index] = key + "=***";
                }
            }

            return route + "?" + string.Join("&", parts);
        }

        private static CottonApiException CreateEmptyJsonException(
            HttpResponseMessage response,
            HttpMethod method,
            string path)
        {
            return new CottonApiException(
                response.StatusCode,
                null,
                $"Cotton API request {FormatRequestLabel(method, path)} returned an empty JSON response.");
        }

        private static string CreateResponsePreview(string responseBody)
        {
            string preview = responseBody
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
            return preview.Length <= ResponsePreviewLength
                ? preview
                : preview[..ResponsePreviewLength] + "...";
        }

        private static string FormatRequestLabel(HttpMethod? method, string? path)
        {
            if (method is null || string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return method.Method + " " + RedactPath(path);
        }

        private static bool IsSensitiveQueryKey(string key)
        {
            return key.Contains("token", StringComparison.OrdinalIgnoreCase)
                || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || key.Contains("password", StringComparison.OrdinalIgnoreCase);
        }
    }
}
