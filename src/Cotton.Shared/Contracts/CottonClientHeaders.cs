// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Shared.Contracts;

/// <summary>
/// Defines Cotton client metadata headers.
/// </summary>
public static class CottonClientHeaders
{
    /// <summary>
    /// Header containing a user-visible device name for issued sessions.
    /// </summary>
    public const string DeviceName = "X-Cotton-Device-Name";

    /// <summary>
    /// Maximum accepted device name length.
    /// </summary>
    public const int DeviceNameMaxLength = 128;
}
