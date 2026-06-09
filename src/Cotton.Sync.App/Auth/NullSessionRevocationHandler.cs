// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// No-op session revocation handler.
    /// </summary>
    public sealed class NullSessionRevocationHandler : ISessionRevocationHandler
    {
        /// <summary>
        /// Gets the singleton no-op instance.
        /// </summary>
        public static NullSessionRevocationHandler Instance { get; } = new();

        private NullSessionRevocationHandler()
        {
        }

        /// <inheritdoc />
        public Task HandleSessionRevokedAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
