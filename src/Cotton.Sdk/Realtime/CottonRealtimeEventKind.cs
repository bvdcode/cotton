// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Realtime;

/// <summary>
/// Defines realtime event categories relevant for SDK consumers.
/// </summary>
public enum CottonRealtimeEventKind
{
    /// <summary>
    /// The event kind is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A file or folder tree mutation occurred.
    /// </summary>
    RemoteFileTreeChanged = 1,

    /// <summary>
    /// The current auth session was revoked by the server.
    /// </summary>
    SessionRevoked = 2,
}
