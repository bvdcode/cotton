// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;

namespace Cotton.Sdk.Auth;

/// <summary>
/// Stores Cotton tokens in memory for tests and short-lived processes.
/// </summary>
public sealed class InMemoryCottonTokenStore : ICottonTokenStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TokenPairDto? _tokens;

    /// <inheritdoc />
    public async Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Clone(_tokens);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _tokens = Clone(tokens);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _tokens = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static TokenPairDto? Clone(TokenPairDto? tokens)
    {
        return tokens is null
            ? null
            : new TokenPairDto
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
            };
    }
}
