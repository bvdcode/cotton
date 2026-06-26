// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Leaves a layout mutation scope when disposed.
    /// </summary>
    internal class LayoutMutationGateLease(
        LayoutMutationGate gate,
        Guid layoutId,
        LayoutMutationGateScope scope) : IAsyncDisposable
    {
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            gate.Exit(layoutId, scope);
            return ValueTask.CompletedTask;
        }
    }
}
