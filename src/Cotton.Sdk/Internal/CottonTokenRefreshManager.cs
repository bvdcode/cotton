// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json;
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

        public async Task RefreshAfterUnauthorizedAsync(
            string? failedAccessToken,
            CancellationToken cancellationToken)
        {
            await _coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                TokenPairDto? tokens = await tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
                if (HasUsableAccessTokenChanged(failedAccessToken, tokens?.AccessToken))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(tokens?.RefreshToken))
                {
                    throw new CottonTokenRefreshException(new InvalidOperationException("A refresh token is required."));
                }

                await RefreshAndSaveAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
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

        private async Task<TokenPairDto> RefreshAndSaveAsync(
            string currentRefreshToken,
            CancellationToken cancellationToken)
        {
            TokenPairDto refreshed;
            try
            {
                refreshed = await refreshToken(currentRefreshToken, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(refreshed.AccessToken)
                    || string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                {
                    throw new InvalidDataException("Token renewal returned an incomplete token pair.");
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or JsonException or InvalidDataException
                || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                logger.LogWarning(exception, "Token renewal could not be completed; preserving stored tokens.");
                throw new CottonTokenRefreshException(exception);
            }

            await tokenStore.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
            _coordinator.RecordRotation(currentRefreshToken, refreshed.RefreshToken);
            return refreshed;
        }

        private static bool HasUsableAccessTokenChanged(string? failedAccessToken, string? currentAccessToken)
        {
            return !string.IsNullOrWhiteSpace(currentAccessToken)
                && !string.Equals(currentAccessToken, failedAccessToken, StringComparison.Ordinal);
        }
    }
}
