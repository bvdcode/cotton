// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cotton;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sdk.Internal
{
    internal class CottonHttpTransport
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly ICottonTokenStore _tokenStore;
        private readonly CottonSdkOptions _options;
        private readonly ILogger<CottonHttpTransport> _logger;
        private readonly CottonHttpSender _sender;
        private readonly CottonTokenRefreshManager _tokenRefreshManager;

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
            _sender = new CottonHttpSender(_httpClient, _logger);
            _tokenRefreshManager = new CottonTokenRefreshManager(tokenStore, SendRefreshRequestAsync, _logger);
            if (_httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = options.BaseAddress;
            }
            else if (!_httpClient.BaseAddress.Equals(options.BaseAddress))
            {
                throw new InvalidOperationException("The supplied HttpClient BaseAddress must match CottonSdkOptions.BaseAddress.");
            }
        }

        public Uri BaseAddress => _options.BaseAddress;

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
            return await ReadRequiredJsonAsync<T>(response, method, path, cancellationToken).ConfigureAwait(false);
        }

        public async Task<CottonPagedResult<T>> SendPagedJsonAsync<T>(
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
            await EnsureSuccessAsync(response, method, path, cancellationToken).ConfigureAwait(false);
            int totalCount = CottonHttpResponseReader.ReadRequiredTotalCount(response, method, path);
            T payload = await ReadRequiredJsonAsync<T>(
                response,
                method,
                path,
                cancellationToken,
                ensureSuccess: false).ConfigureAwait(false);
            return new CottonPagedResult<T>(payload, totalCount);
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
            await EnsureSuccessAsync(response, method, path, cancellationToken).ConfigureAwait(false);
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
            string? requestAccessToken = request.Headers.Authorization?.Parameter;
            HttpResponseMessage response = await _sender.SendAsync(request, method, path, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized || !authorize || !_options.RefreshOnUnauthorized)
            {
                return response;
            }

            response.Dispose();
            await RefreshAndLogRetryAsync(method, path, requestAccessToken, cancellationToken).ConfigureAwait(false);

            using HttpRequestMessage retry = await CreateRequestAsync(
                method,
                path,
                body,
                authorize,
                headers,
                cancellationToken).ConfigureAwait(false);
            return await _sender.SendAsync(retry, method, path, cancellationToken).ConfigureAwait(false);
        }

        public async Task UploadRawAsync(
            string path,
            Stream content,
            string contentType,
            bool authorize,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(content);
            long retryPosition = content.CanSeek ? content.Position : 0;
            var (response, requestAccessToken) = await SendRawUploadOnceAsync(
                path,
                content,
                contentType,
                authorize,
                cancellationToken).ConfigureAwait(false);
            using (response)
            {
                if (response.StatusCode != HttpStatusCode.Unauthorized || !authorize || !_options.RefreshOnUnauthorized || !content.CanSeek)
                {
                    await EnsureSuccessAsync(response, HttpMethod.Post, path, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            await RefreshAndLogRetryAsync(HttpMethod.Post, path, requestAccessToken, cancellationToken).ConfigureAwait(false);
            content.Position = retryPosition;
            var (retry, _) = await SendRawUploadOnceAsync(
                path,
                content,
                contentType,
                authorize,
                cancellationToken).ConfigureAwait(false);
            using (retry)
            {
                await EnsureSuccessAsync(retry, HttpMethod.Post, path, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<(HttpResponseMessage Response, string? AccessToken)> SendRawUploadOnceAsync(
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
            string? accessToken = request.Headers.Authorization?.Parameter;
            try
            {
                request.Content = new StreamContent(content);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                HttpResponseMessage response = await _sender.SendAsync(
                    request,
                    HttpMethod.Post,
                    path,
                    cancellationToken).ConfigureAwait(false);
                request.Content = null;
                return (response, accessToken);
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
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string>? headers = null,
            HttpStatusCode? expectedStatusCode = null,
            Func<HttpResponseMessage, long?>? validateResponse = null)
        {
            ArgumentNullException.ThrowIfNull(destination);
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                path,
                body: null,
                authorize: authorize,
                headers: headers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (expectedStatusCode.HasValue)
            {
                await CottonHttpResponseReader.EnsureExpectedStatusAsync(
                    response,
                    HttpMethod.Get,
                    path,
                    expectedStatusCode.Value,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await EnsureSuccessAsync(response, HttpMethod.Get, path, cancellationToken).ConfigureAwait(false);
            }

            long? expectedBodyLength = validateResponse?.Invoke(response);
            await CottonHttpDownloadWriter.CopyAsync(
                response,
                destination,
                path,
                progress,
                expectedBodyLength,
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await CottonHttpResponseReader
                .EnsureSuccessAsync(response, method: null, path: null, cancellationToken)
                .ConfigureAwait(false);
        }

        public static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            HttpMethod? method,
            string? path,
            CancellationToken cancellationToken)
        {
            await CottonHttpResponseReader
                .EnsureSuccessAsync(response, method, path, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(
            HttpMethod method,
            string path,
            object? body,
            bool authorize,
            IReadOnlyDictionary<string, string>? headers,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage request = new(method, CottonRouteUri.Create(_options.BaseAddress, path));
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

        internal async Task<T> ReadRequiredJsonAsync<T>(
            HttpResponseMessage response,
            HttpMethod method,
            string path,
            CancellationToken cancellationToken,
            bool ensureSuccess = true)
        {
            return await CottonHttpResponseReader.ReadRequiredJsonAsync<T>(
                response,
                method,
                path,
                cancellationToken,
                ensureSuccess).ConfigureAwait(false);
        }

        internal Task<TokenPairDto> RefreshTokenAsync(
            string? refreshToken,
            CancellationToken cancellationToken)
        {
            return _tokenRefreshManager.RefreshAsync(refreshToken, cancellationToken);
        }

        internal Task SaveTokenPairAsync(TokenPairDto tokens, CancellationToken cancellationToken)
        {
            return _tokenRefreshManager.SaveAsync(tokens, cancellationToken);
        }

        internal Task ClearTokenPairAsync(CancellationToken cancellationToken)
        {
            return _tokenRefreshManager.ClearAsync(cancellationToken);
        }

        private async Task<TokenPairDto> SendRefreshRequestAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            string path = Routes.V1.Auth + "/refresh?refreshToken=" + Uri.EscapeDataString(refreshToken);
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                CottonRouteUri.Create(_options.BaseAddress, path));
            ApplyDefaultHeaders(request);
            using HttpResponseMessage response = await _sender.SendAsync(
                request,
                HttpMethod.Post,
                path,
                cancellationToken).ConfigureAwait(false);
            return await ReadRequiredJsonAsync<TokenPairDto>(
                response,
                HttpMethod.Post,
                path,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task RefreshAndLogRetryAsync(
            HttpMethod method,
            string path,
            string? failedAccessToken,
            CancellationToken cancellationToken)
        {
            bool refreshed = await _tokenRefreshManager
                .TryRefreshAsync(failedAccessToken, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogWarning(
                "Cotton API request {Method} {Path} returned unauthorized; token refresh {RefreshResult}, retrying request.",
                method.Method,
                CottonHttpResponseReader.RedactPath(path),
                refreshed ? "succeeded" : "failed");
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

    }
}
