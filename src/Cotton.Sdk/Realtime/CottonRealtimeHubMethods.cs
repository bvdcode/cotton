// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Realtime;

/// <summary>
/// Defines SignalR event hub method names emitted by the Cotton server.
/// </summary>
public static class CottonRealtimeHubMethods
{
    /// <summary>
    /// Server method sent when the current auth session is revoked.
    /// </summary>
    public const string SessionRevoked = "SessionRevoked";

    /// <summary>
    /// Server methods that indicate file-tree mutations.
    /// </summary>
    public static readonly IReadOnlyList<string> RemoteFileTreeChanged =
    [
        "FileCreated",
        "FileUpdated",
        "FileDeleted",
        "FileMoved",
        "FileRenamed",
        "FileRestored",
        "NodeCreated",
        "NodeDeleted",
        "NodeMoved",
        "NodeRenamed",
        "NodeRestored",
    ];
}
