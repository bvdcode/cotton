// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Serializes namespace mutations within one layout.
    /// </summary>
    public interface ILayoutMutationGate
    {
        /// <summary>
        /// Enters the mutation gate for a layout.
        /// </summary>
        Task<IAsyncDisposable> EnterAsync(Guid layoutId, CancellationToken cancellationToken);
    }
}
