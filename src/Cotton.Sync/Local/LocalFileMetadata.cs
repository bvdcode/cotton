// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local
{
    internal readonly record struct LocalFileMetadata(long Length, DateTime LastWriteUtc);
}
