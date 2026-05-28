// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents the response after adding a recovery recipient slot.
/// </summary>
public sealed class KeyringCreateRecoverySlotResponseDto
{
    /// <summary>
    /// Gets or sets the created recovery slot id.
    /// </summary>
    public string SlotId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the keyring root epoch.
    /// </summary>
    public int RootEpoch { get; init; }

    /// <summary>
    /// Gets or sets the committed access envelope generation.
    /// </summary>
    public int AccessGeneration { get; init; }

    /// <summary>
    /// Gets or sets the current state snapshot generation.
    /// </summary>
    public int StateGeneration { get; init; }

    /// <summary>
    /// Gets or sets the updated encrypted recovery kit.
    /// </summary>
    public KeyringRecoveryKitDto RecoveryKit { get; init; } = new();
}
