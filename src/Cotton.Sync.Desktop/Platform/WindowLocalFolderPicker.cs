// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Cotton.Sync.Desktop.Platform
{

    internal sealed class WindowLocalFolderPicker : ILocalFolderPicker
    {
        private readonly Window _owner;

        public WindowLocalFolderPicker(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IStorageFolder> folders = await _owner.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "Select local sync folder",
                }).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        }
    }
}
