// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Internal
{
    internal class CottonTokenRefreshManager(
        ICottonTokenStore tokenStore,
        Func<string, CancellationToken, Task<TokenPairDto>> refreshToken,
        ILogger logger)
    {
        private readonly CottonTokenRefreshCoordinator _coordinator = CottonTokenRefreshCoordinator.Get(tokenStore);

        public async Task<bool> TryRefreshAsync(
            string? failedAccessToken,
            CancellationToken cancellationToken)
        {
            await _coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                TokenPairDto? tokens = await tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
                if (HasUsableAccessTokenChanged(failedAccessToken, tokens?.AccessToken))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(tokens?.RefreshToken))
                {
                    return false;
                }

                return await TryRefreshCoreAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _coordinator.Release();
            }
        }

        public async Task<TokenPairDto> RefreshAsync(
            string? requestedRefreshToken,
            CancellationToken cancellationToken)
        {
            await _coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                TokenPairDto? stored = await tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
                string? effectiveRefreshToken = string.IsNullOrWhiteSpace(requestedRefreshToken)
                    ? stored?.RefreshToken
                    : requestedRefreshToken;
                if (string.IsNullOrWhiteSpace(effectiveRefreshToken))
                {
                    throw new InvalidOperationException("A refresh token is required.");
                }

                if (stored is not null
                    && _coordinator.WasRotated(effectiveRefreshToken, stored.RefreshToken))
                {
                    return stored;
                }

                return await RefreshAndSaveAsync(effectiveRefreshToken, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _coordinator.Release();
            }
        }

        public async Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            await _coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await tokenStore.SaveAsync(tokens, cancellationToken).ConfigureAwait(false);
                _coordinator.ResetRotation();
            }
            finally
            {
                _coordinator.Release();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            await _coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                _coordinator.ResetRotation();
            }
            finally
            {
                _coordinator.Release();
            }
        }

        private async Task<bool> TryRefreshCoreAsync(
            string currentRefreshToken,
            CancellationToken cancellationToken)
        {
            try
            {
                await RefreshAndSaveAsync(currentRefreshToken, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (CottonApiException exception) when (InvalidatesRefreshToken(exception.StatusCode))
            {
                logger.LogWarning(
                    "Cotton API token refresh was rejected with status {StatusCode}; clearing the rejected token pair.",
                    (int?)exception.StatusCode);
                await tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                _coordinator.ResetRotation();
                return false;
            }
            catch (CottonApiException exception)
            {
                logger.LogWarning(
                    "Cotton API token refresh failed with transient status {StatusCode}; preserving stored tokens.",
                    (int?)exception.StatusCode);
                return false;
            }
        }

        private async Task<TokenPairDto> RefreshAndSaveAsync(
            string currentRefreshToken,
            CancellationToken cancellationToken)
        {
            TokenPairDto refreshed = await refreshToken(currentRefreshToken, cancellationToken).ConfigureAwait(false);
            await tokenStore.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
            _coordinator.RecordRotation(currentRefreshToken, refreshed.RefreshToken);
            return refreshed;
        }

        private static bool HasUsableAccessTokenChanged(string? failedAccessToken, string? currentAccessToken)
        {
            return !string.IsNullOrWhiteSpace(currentAccessToken)
                && !string.Equals(currentAccessToken, failedAccessToken, StringComparison.Ordinal);
        }

        private static bool InvalidatesRefreshToken(HttpStatusCode? statusCode)
        {
            return statusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.NotFound;
        }
    }
}
