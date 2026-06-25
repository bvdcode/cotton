// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using System.Collections.Concurrent;

namespace Cotton.Server.Services
{
    /// <summary>
    /// In-process semaphore gate for layout namespace mutations.
    /// </summary>
    public class LayoutMutationGate : ILayoutMutationGate
    {
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

        /// <inheritdoc />
        public async Task<IAsyncDisposable> EnterAsync(Guid layoutId, CancellationToken cancellationToken)
        {
            SemaphoreSlim gate = _gates.GetOrAdd(layoutId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            return new LayoutMutationGateLease(gate);
        }
    }
}
