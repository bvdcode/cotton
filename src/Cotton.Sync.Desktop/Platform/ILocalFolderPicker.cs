// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal interface ILocalFolderPicker
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
