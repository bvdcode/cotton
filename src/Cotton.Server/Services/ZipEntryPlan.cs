// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal record ZipEntryPlan(
        string Path, byte[] PathBytes, long SizeBytes, bool IsDirectory,
        bool UsesZip64DataDescriptor, long LocalHeaderOffset)
    {
        public long CentralExtraLength { get; init; }
    }
}
