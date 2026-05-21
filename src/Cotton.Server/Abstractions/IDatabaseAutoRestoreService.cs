// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    public interface IDatabaseAutoRestoreService
    {
        Task TryRestoreIfEmptyAsync(CancellationToken cancellationToken = default);
    }
}
