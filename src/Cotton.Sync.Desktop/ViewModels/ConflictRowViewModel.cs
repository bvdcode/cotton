// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels
{
    internal sealed class ConflictRowViewModel
    {
        public Guid? SyncPairId { get; init; }

        public string Time { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public string Details { get; init; } = string.Empty;
    }
}
