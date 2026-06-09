// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels
{

    internal sealed class RemoteFolderRowViewModel : ViewModelBase
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;
    }
}
