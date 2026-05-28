// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents the result of restoring replicated keyring objects from a recovery kit.
/// </summary>
public sealed class KeyringRecoveryKitImportResponseDto
{
    /// <summary>
    /// Gets the UTC time when the kit was imported.
    /// </summary>
    public DateTimeOffset ImportedAtUtc { get; init; }

    /// <summary>
    /// Gets the restored keyring root epoch.
    /// </summary>
    public int RootEpoch { get; init; }

    /// <summary>
    /// Gets the restored access envelope generation.
    /// </summary>
    public int AccessGeneration { get; init; }

    /// <summary>
    /// Gets the restored state snapshot generation.
    /// </summary>
    public int StateGeneration { get; init; }

    /// <summary>
    /// Gets the restored access envelope hash.
    /// </summary>
    public string AccessEnvelopeHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the restored state snapshot hash.
    /// </summary>
    public string StateSnapshotHash { get; init; } = string.Empty;
}
