// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Auth;

/// <summary>
/// Describes a server-driven authentication session revocation event.
/// </summary>
public sealed class SessionRevocationEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRevocationEvent" /> class.
    /// </summary>
    public SessionRevocationEvent(DateTime occurredAtUtc)
    {
        OccurredAtUtc = UtcDateTime.Normalize(occurredAtUtc);
    }

    /// <summary>
    /// Gets the UTC timestamp when the revocation was handled locally.
    /// </summary>
    public DateTime OccurredAtUtc { get; }
}
