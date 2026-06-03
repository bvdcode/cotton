// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Realtime;

/// <summary>
/// Defines realtime event categories relevant for SDK consumers.
/// </summary>
public enum CottonRealtimeEventKind
{
    /// <summary>
    /// A file or folder tree mutation occurred.
    /// </summary>
    RemoteFileTreeChanged = 0,

    /// <summary>
    /// The current auth session was revoked by the server.
    /// </summary>
    SessionRevoked = 1,
}
