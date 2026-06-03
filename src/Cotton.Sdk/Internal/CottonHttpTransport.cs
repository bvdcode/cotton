// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sdk.Internal;

internal sealed class CottonHttpTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ICottonTokenStore _tokenStore;
    private readonly CottonSdkOptions _options;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public CottonHttpTransport(HttpClient httpClient, ICottonTokenStore tokenStore, CottonSdkOptions options)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _options = options;
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
        HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !authorize || !_options.RefreshOnUnauthorized)
        {
            return response;
        }

        response.Dispose();
        _ = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);

        using HttpRequestMessage retry = await CreateRequestAsync(
            method,
            path,
            body,
            authorize,
            headers,
            cancellationToken).ConfigureAwait(false);
        return await _httpClient.SendAsync(
            retry,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
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
        _ = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
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
            HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
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
        throw new CottonApiException(
            response.StatusCode,
            body,
            $"Cotton API request failed with status {(int)response.StatusCode} ({response.StatusCode}).");
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
        T? result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new CottonApiException(response.StatusCode, null, "Cotton API returned an empty JSON response.");
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
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
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
}
