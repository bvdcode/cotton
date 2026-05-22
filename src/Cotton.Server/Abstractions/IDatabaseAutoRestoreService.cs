// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the database auto restore service contract used by the server runtime.
    /// </summary>
    public interface IDatabaseAutoRestoreService
    {
        /// <summary>
        /// Attempts to restore if empty.
        /// </summary>
        Task TryRestoreIfEmptyAsync(CancellationToken cancellationToken = default);
    }
}
