// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class UserStorageQuotaMutationGate
    {
        private readonly KeyedAsyncGate<Guid> _gates = new();

        public ValueTask<IAsyncDisposable> EnterAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _gates.EnterAsync(userId, cancellationToken);
        }

        internal int Count => _gates.Count;
    }
}
