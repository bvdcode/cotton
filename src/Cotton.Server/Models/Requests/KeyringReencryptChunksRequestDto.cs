// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests;

/// <summary>
/// Requests a resumable keyring chunk re-encryption batch.
/// </summary>
public sealed class KeyringReencryptChunksRequestDto
{
    /// <summary>Zero-based chunk scan offset.</summary>
    public int Offset { get; init; }

    /// <summary>Maximum number of chunks to inspect in this batch.</summary>
    public int Limit { get; init; } = 100;
}
