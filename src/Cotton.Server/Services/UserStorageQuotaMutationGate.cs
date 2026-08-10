// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Serializes final storage-quota mutations for each user within this server process.
    /// </summary>
    public class UserStorageQuotaMutationGate
    {
        private readonly KeyedAsyncGate<Guid> _gates = new();

        /// <summary>
        /// Waits until the caller exclusively owns the final quota-mutation section for the user.
        /// </summary>
        public ValueTask<IAsyncDisposable> EnterAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _gates.EnterAsync(userId, cancellationToken);
        }

        internal int Count => _gates.Count;
    }
}
