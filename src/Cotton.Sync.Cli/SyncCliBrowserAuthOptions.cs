// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli
{
    internal record SyncCliBrowserAuthOptions(
        Uri ServerUri,
        string ApplicationName,
        string? ApplicationVersion,
        string? DeviceName);
}
