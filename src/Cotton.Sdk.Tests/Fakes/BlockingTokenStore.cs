// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class BlockingTokenStore(ICottonTokenStore inner) : ICottonTokenStore
    {
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool PauseSave { get; set; }
        public bool FailSave { get; set; }

        public Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            return inner.GetAsync(cancellationToken);
        }

        public async Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default)
        {
            SaveStarted.TrySetResult();
            if (PauseSave)
            {
                await ReleaseSave.Task.WaitAsync(cancellationToken);
            }

            if (FailSave)
            {
                throw new IOException("Token persistence failed.");
            }

            await inner.SaveAsync(tokens, cancellationToken);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return inner.ClearAsync(cancellationToken);
        }
    }
}
