// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Internal;
using Cotton;
using System.Net;

namespace Cotton.Sdk.Auth
{
    /// <summary>
    /// Provides authentication operations for Cotton Cloud.
    /// </summary>
    public class CottonAuthClient : ICottonAuthClient
    {
        private readonly CottonHttpTransport _transport;
        private readonly ICottonTokenStore _tokenStore;

        internal CottonAuthClient(CottonHttpTransport transport, ICottonTokenStore tokenStore)
        {
            _transport = transport;
            _tokenStore = tokenStore;
        }

        /// <summary>
        /// Authenticates with username and password and stores the issued token pair.
        /// </summary>
        public async Task<TokenPairDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            TokenPairDto tokens = await _transport.SendJsonAsync<TokenPairDto>(
                HttpMethod.Post,
                Routes.V1.Auth + "/login",
                request,
                authorize: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await _tokenStore.SaveAsync(tokens, cancellationToken).ConfigureAwait(false);
            return tokens;
        }

        /// <summary>
        /// Starts a browser app-code authorization session.
        /// </summary>
        public async Task<AppCodeAuthorizationSession> StartAppCodeAsync(
            AppCodeStartRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            AppCodeStartResponseDto response = await _transport.SendJsonAsync<AppCodeStartResponseDto>(
                HttpMethod.Post,
                Routes.V1.AppCodeOAuth + "/start",
                request,
                authorize: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new AppCodeAuthorizationSession
            {
                ApprovalId = response.ApprovalId,
                ApprovalUri = CottonRouteUri.Create(_transport.BaseAddress, response.ApprovalUrl),
                PollToken = response.PollToken,
                ExpiresAt = response.ExpiresAt,
                PollInterval = TimeSpan.FromSeconds(response.PollIntervalSeconds),
            };
        }

        /// <summary>
        /// Polls a browser app-code authorization session and stores issued tokens when approved.
        /// </summary>
        public async Task<AppCodePollResult> PollAppCodeAsync(
            string pollToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pollToken))
            {
                throw new ArgumentException("A poll token is required.", nameof(pollToken));
            }

            string path = Routes.V1.AppCodeOAuth + "/poll";
            using HttpResponseMessage response = await _transport.SendAsync(
                HttpMethod.Post,
                path,
                new AppCodePollRequestDto { PollToken = pollToken },
                authorize: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                TokenPairDto tokens = await _transport.ReadRequiredJsonAsync<TokenPairDto>(
                    response,
                    HttpMethod.Post,
                    path,
                    cancellationToken).ConfigureAwait(false);
                await _tokenStore.SaveAsync(tokens, cancellationToken).ConfigureAwait(false);
                return new AppCodePollResult
                {
                    Status = AppCodePollStatus.Approved,
                    Tokens = tokens,
                };
            }

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return await ReadPollErrorAsync(
                    response,
                    AppCodePollStatus.Pending,
                    cancellationToken).ConfigureAwait(false);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return await ReadPollErrorAsync(
                    response,
                    AppCodePollStatus.Denied,
                    cancellationToken).ConfigureAwait(false);
            }

            if (response.StatusCode == HttpStatusCode.Gone)
            {
                return await ReadPollErrorAsync(
                    response,
                    AppCodePollStatus.Expired,
                    cancellationToken).ConfigureAwait(false);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return await ReadPollErrorAsync(
                    response,
                    AppCodePollStatus.NotFound,
                    cancellationToken).ConfigureAwait(false);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return await ReadPollErrorAsync(
                    response,
                    AppCodePollStatus.TooManyRequests,
                    cancellationToken).ConfigureAwait(false);
            }

            await CottonHttpTransport.EnsureSuccessAsync(
                response,
                HttpMethod.Post,
                path,
                cancellationToken).ConfigureAwait(false);
            return new AppCodePollResult { Status = AppCodePollStatus.Unknown };
        }

        /// <summary>
        /// Refreshes an access token using a stored or explicit refresh token.
        /// </summary>
        public async Task<TokenPairDto> RefreshAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                TokenPairDto? stored = await _tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
                refreshToken = stored?.RefreshToken;
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new InvalidOperationException("A refresh token is required.");
            }

            TokenPairDto tokens = await _transport.SendJsonAsync<TokenPairDto>(
                HttpMethod.Post,
                Routes.V1.Auth + "/refresh?refreshToken=" + Uri.EscapeDataString(refreshToken),
                authorize: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await _tokenStore.SaveAsync(tokens, cancellationToken).ConfigureAwait(false);
            return tokens;
        }

        /// <summary>
        /// Revokes a refresh token and clears the token store.
        /// </summary>
        public async Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                TokenPairDto? stored = await _tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
                refreshToken = stored?.RefreshToken;
            }

            string path = string.IsNullOrWhiteSpace(refreshToken)
                ? Routes.V1.Auth + "/logout"
                : Routes.V1.Auth + "/logout?refreshToken=" + Uri.EscapeDataString(refreshToken);
            try
            {
                await _transport.SendNoContentAsync(
                    HttpMethod.Post,
                    path,
                    authorize: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the current authenticated user.
        /// </summary>
        public Task<UserDto> MeAsync(CancellationToken cancellationToken = default)
        {
            return _transport.SendJsonAsync<UserDto>(
                HttpMethod.Get,
                Routes.V1.Auth + "/me",
                cancellationToken: cancellationToken);
        }

        private async Task<AppCodePollResult> ReadPollErrorAsync(
            HttpResponseMessage response,
            AppCodePollStatus status,
            CancellationToken cancellationToken)
        {
            AppCodePollErrorDto error = await _transport.ReadRequiredJsonAsync<AppCodePollErrorDto>(
                response,
                HttpMethod.Post,
                Routes.V1.AppCodeOAuth + "/poll",
                cancellationToken,
                ensureSuccess: false).ConfigureAwait(false);
            return new AppCodePollResult
            {
                Status = status,
                Error = error.Error,
                RetryAfter = error.RetryAfterSeconds.HasValue
                    ? TimeSpan.FromSeconds(error.RetryAfterSeconds.Value)
                    : null,
            };
        }
    }
}
