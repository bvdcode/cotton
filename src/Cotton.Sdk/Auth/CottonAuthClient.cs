// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Auth;
using Cotton.Sdk.Internal;

namespace Cotton.Sdk.Auth;

/// <summary>
/// Provides authentication operations for Cotton Cloud.
/// </summary>
public sealed class CottonAuthClient : ICottonAuthClient
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
            "/api/v1/auth/login",
            request,
            authorize: false,
            cancellationToken).ConfigureAwait(false);
        await _tokenStore.SaveAsync(tokens, cancellationToken).ConfigureAwait(false);
        return tokens;
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
            "/api/v1/auth/refresh?refreshToken=" + Uri.EscapeDataString(refreshToken),
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
            ? "/api/v1/auth/logout"
            : "/api/v1/auth/logout?refreshToken=" + Uri.EscapeDataString(refreshToken);
        await _transport.SendNoContentAsync(
            HttpMethod.Post,
            path,
            authorize: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    public Task<UserDto> MeAsync(CancellationToken cancellationToken = default)
    {
        return _transport.SendJsonAsync<UserDto>(
            HttpMethod.Get,
            "/api/v1/auth/me",
            cancellationToken: cancellationToken);
    }
}
