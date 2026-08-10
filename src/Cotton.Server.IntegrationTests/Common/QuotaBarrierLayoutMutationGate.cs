// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Services;

namespace Cotton.Server.IntegrationTests.Common
{
    internal class QuotaBarrierLayoutMutationGate(QuotaMutationBarrier _barrier) : ILayoutMutationGate
    {
        private readonly KeyedAsyncGate<Guid> _inner = new();

        public async Task<IAsyncDisposable> EnterAsync(
            Guid layoutId,
            CancellationToken cancellationToken)
        {
            IAsyncDisposable lease = await _inner.EnterAsync(layoutId, cancellationToken);
            try
            {
                await _barrier.SignalAndWaitIfEnabledAsync(cancellationToken);
                return lease;
            }
            catch
            {
                await lease.DisposeAsync();
                throw;
            }
        }
    }
}
