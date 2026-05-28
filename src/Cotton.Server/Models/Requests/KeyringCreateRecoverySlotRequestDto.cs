// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests;

/// <summary>
/// Represents a request to add a recovery recipient slot to the keyring.
/// </summary>
public sealed class KeyringCreateRecoverySlotRequestDto
{
    /// <summary>
    /// Gets or sets the current unlock secret used to authorize wrapping the root key.
    /// </summary>
    public string CurrentUnlockSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the high-entropy recovery secret derived from the recovery phrase.
    /// </summary>
    public string RecoverySecret { get; init; } = string.Empty;
}
