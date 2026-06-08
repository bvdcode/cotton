// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Platform;

namespace Cotton.Sync.Cli
{
    internal class SyncCliApprovalUrlWriter : IPlatformCommandService
    {
        private readonly TextWriter _output;

        public SyncCliApprovalUrlWriter(TextWriter output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The CLI browser authentication command does not open folders.");
        }

        public async Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(url);
            await _output.WriteLineAsync("Approval URL: " + url.AbsoluteUri).ConfigureAwait(false);
        }
    }
}
