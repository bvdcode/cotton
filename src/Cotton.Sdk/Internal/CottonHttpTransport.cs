// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cotton.Contracts;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sdk.Internal;

internal sealed class CottonHttpTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int ResponsePreviewLength = 180;

    private readonly HttpClient _httpClient;
    private readonly ICottonTokenStore _tokenStore;
    private readonly CottonSdkOptions _options;
    private readonly ILogger<CottonHttpTransport> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public CottonHttpTransport(
        HttpClient httpClient,
        ICottonTokenStore tokenStore,
        CottonSdkOptions options,
        ILogger<CottonHttpTransport>? logger = null)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _options = options;
        _logger = logger ?? NullLogger<CottonHttpTransport>.Instance;
        _httpClient.BaseAddress = options.BaseAddress;
    }

    public async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        bool authorize = true,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            method,
            path,
            body,
            authorize,
            headers,
            cancellationToken).ConfigureAwait(false);
        return await ReadRequiredJsonAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendNoContentAsync(
        HttpMethod method,
        string path,
        object? body = null,
        bool authorize = true,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            method,
            path,
            body,
            authorize,
            headers,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        bool authorize = true,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = await CreateRequestAsync(
            method,
            path,
            body,
            authorize,
            headers,
            cancellationToken).ConfigureAwait(false);
        HttpResponseMessage response = await SendHttpAsync(request, method, path, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !authorize || !_options.RefreshOnUnauthorized)
        {
            return response;
        }

        response.Dispose();
        await RefreshAndLogRetryAsync(method, path, cancellationToken).ConfigureAwait(false);

        using HttpRequestMessage retry = await CreateRequestAsync(
            method,
            path,
            body,
            authorize,
            headers,
            cancellationToken).ConfigureAwait(false);
        return await SendHttpAsync(retry, method, path, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadRawAsync(
        string path,
        Stream content,
        string contentType,
        bool authorize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using HttpResponseMessage response = await SendRawUploadOnceAsync(
            path,
            content,
            contentType,
            authorize,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !authorize || !_options.RefreshOnUnauthorized || !content.CanSeek)
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return;
        }

        response.Dispose();
        await RefreshAndLogRetryAsync(HttpMethod.Post, path, cancellationToken).ConfigureAwait(false);
        content.Position = 0;
        using HttpResponseMessage retry = await SendRawUploadOnceAsync(
            path,
            content,
            contentType,
            authorize,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendRawUploadOnceAsync(
        string path,
        Stream content,
        string contentType,
        bool authorize,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage request = await CreateRequestAsync(
            HttpMethod.Post,
            path,
            body: null,
            authorize: authorize,
            headers: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            request.Content = new StreamContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            HttpResponseMessage response = await SendHttpAsync(
                request,
                HttpMethod.Post,
                path,
                cancellationToken).ConfigureAwait(false);
            request.Content = null;
            return response;
        }
        finally
        {
            request.Dispose();
        }
    }

    public async Task DownloadAsync(
        string path,
        Stream destination,
        bool authorize,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            path,
            body: null,
            authorize: authorize,
            headers: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            progress?.Report(total);
        }
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string message = $"Cotton API request failed with status {(int)response.StatusCode} ({response.StatusCode}).";
        if (!string.IsNullOrWhiteSpace(body))
        {
            message += " Response: " + CreateResponsePreview(body);
        }

        throw new CottonApiException(
            response.StatusCode,
            body,
            message);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        object? body,
        bool authorize,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        ApplyDefaultHeaders(request);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (authorize)
        {
            TokenPairDto? tokens = await _tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(tokens?.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            }
        }

        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }

        return request;
    }

    private async Task<T> ReadRequiredJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new CottonApiException(response.StatusCode, null, "Cotton API returned an empty JSON response.");
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
                "Cotton API returned invalid JSON"
                + $" with content type '{contentType}' and status {(int)response.StatusCode} ({response.StatusCode})."
                + " Response: "
                + CreateResponsePreview(body),
                exception);
        }

        return result ?? throw new CottonApiException(response.StatusCode, null, "Cotton API returned an empty JSON response.");
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

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TokenPairDto? tokens = await _tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tokens?.RefreshToken))
            {
                return false;
            }

            string path = "/api/v1/auth/refresh?refreshToken=" + Uri.EscapeDataString(tokens.RefreshToken);
            using HttpRequestMessage request = new(HttpMethod.Post, path);
            ApplyDefaultHeaders(request);
            using HttpResponseMessage response = await SendHttpAsync(
                request,
                HttpMethod.Post,
                path,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Cotton API token refresh failed with status {StatusCode}; clearing stored tokens.",
                    (int)response.StatusCode);
                await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            TokenPairDto refreshed = await ReadRequiredJsonAsync<TokenPairDto>(response, cancellationToken).ConfigureAwait(false);
            await _tokenStore.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshAndLogRetryAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        bool refreshed = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning(
            "Cotton API request {Method} {Path} returned unauthorized; token refresh {RefreshResult}, retrying request.",
            method.Method,
            RedactPath(path),
            refreshed ? "succeeded" : "failed");
    }

    private async Task<HttpResponseMessage> SendHttpAsync(
        HttpRequestMessage request,
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        string redactedPath = RedactPath(path);
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

    private void ApplyDefaultHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        }

        string? deviceName = NormalizeDeviceName(_options.DeviceName);
        if (deviceName is not null)
        {
            request.Headers.TryAddWithoutValidation(CottonClientHeaders.DeviceName, deviceName);
        }
    }

    private static string? NormalizeDeviceName(string? value)
    {
        string? normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return normalized.Length <= CottonClientHeaders.DeviceNameMaxLength
            ? normalized
            : normalized[..CottonClientHeaders.DeviceNameMaxLength];
    }

    private static string RedactPath(string path)
    {
        int queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0 || queryIndex == path.Length - 1)
        {
            return path;
        }

        string route = path[..queryIndex];
        string query = path[(queryIndex + 1)..];
        string[] parts = query.Split('&');
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

    private static bool IsSensitiveQueryKey(string key)
    {
        return key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase);
    }
}
