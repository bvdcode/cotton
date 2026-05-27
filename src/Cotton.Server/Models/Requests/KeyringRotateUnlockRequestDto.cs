// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests;

/// <summary>
/// Represents the keyring unlock-secret rotation request payload accepted by the API.
/// </summary>
public sealed class KeyringRotateUnlockRequestDto
{
    /// <summary>
    /// Gets or sets the current keyring unlock secret.
    /// </summary>
    public string CurrentUnlockSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new keyring unlock secret.
    /// </summary>
    public string NewUnlockSecret { get; set; } = string.Empty;
}
