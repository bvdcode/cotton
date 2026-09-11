// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.IO.Compression;

namespace Cotton.Previews
{
    internal record AndroidPackageIconEntryCandidate(ZipArchiveEntry Entry, int Score);
}
