// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Represents an exportable encrypted keyring recovery kit.
/// </summary>
public sealed class KeyringRecoveryKitDto
{
    /// <summary>
    /// Gets the recovery kit format marker.
    /// </summary>
    public string Magic { get; init; } = "cotton.recovery-kit.v2";

    /// <summary>
    /// Gets the keyring schema version.
    /// </summary>
    public int Schema { get; init; } = 2;

    /// <summary>
    /// Gets the UTC time when the kit was exported.
    /// </summary>
    public DateTimeOffset ExportedAtUtc { get; init; }

    /// <summary>
    /// Gets the Cotton instance identifier bound into the keyring objects.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets the stable keyring identifier.
    /// </summary>
    public string KeyringId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the keyring root epoch included in this kit.
    /// </summary>
    public int RootEpoch { get; init; }

    /// <summary>
    /// Gets the access envelope generation.
    /// </summary>
    public int AccessGeneration { get; init; }

    /// <summary>
    /// Gets the state snapshot generation.
    /// </summary>
    public int StateGeneration { get; init; }

    /// <summary>
    /// Gets the immutable access envelope object name.
    /// </summary>
    public string AccessEnvelopeObjectName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SHA-256 hash of the access envelope bytes.
    /// </summary>
    public string AccessEnvelopeHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the encrypted access envelope bytes encoded as base64.
    /// </summary>
    public string AccessEnvelopeBase64 { get; init; } = string.Empty;

    /// <summary>
    /// Gets the immutable state snapshot object name.
    /// </summary>
    public string StateSnapshotObjectName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SHA-256 hash of the encrypted state snapshot bytes.
    /// </summary>
    public string StateSnapshotHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the encrypted state snapshot bytes encoded as base64.
    /// </summary>
    public string StateSnapshotBase64 { get; init; } = string.Empty;
}
