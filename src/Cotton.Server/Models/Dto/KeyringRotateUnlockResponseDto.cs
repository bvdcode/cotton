// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents the keyring unlock-secret rotation response payload.
/// </summary>
public sealed class KeyringRotateUnlockResponseDto
{
    /// <summary>
    /// Gets or sets the current keyring root epoch after rotation.
    /// </summary>
    public int RootEpoch { get; init; }

    /// <summary>
    /// Gets or sets the committed access envelope generation.
    /// </summary>
    public int AccessGeneration { get; init; }

    /// <summary>
    /// Gets or sets the committed state snapshot generation.
    /// </summary>
    public int StateGeneration { get; init; }
}
