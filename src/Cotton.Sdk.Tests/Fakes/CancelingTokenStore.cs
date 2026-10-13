// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class CancelingTokenStore(
        InMemoryCottonTokenStore inner,
        CancellationTokenSource cancellation) : ICottonTokenStore
    {
        public bool CancelBeforeSave { get; set; }

        public Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            return inner.GetAsync(cancellationToken);
        }

        public async Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default)
        {
            if (CancelBeforeSave)
            {
                CancelBeforeSave = false;
                await cancellation.CancelAsync();
            }

            await inner.SaveAsync(tokens, cancellationToken);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return inner.ClearAsync(cancellationToken);
        }
    }
}
