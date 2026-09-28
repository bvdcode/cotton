// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal static class ArchiveTemporaryFile
    {
        public static FileStream Create()
        {
            return new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose,
                BufferSize = 64 * 1024,
            });
        }
    }
}
