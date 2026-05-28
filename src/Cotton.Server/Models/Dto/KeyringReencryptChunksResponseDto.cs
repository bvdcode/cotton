// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>
/// Result of one resumable keyring chunk re-encryption batch.
/// </summary>
public sealed class KeyringReencryptChunksResponseDto
{
    /// <summary>Primary chunk key id used for rewritten chunks.</summary>
    public int TargetKeyId { get; init; }

    /// <summary>Offset used for this batch.</summary>
    public int Offset { get; init; }

    /// <summary>Offset to use for the next batch.</summary>
    public int NextOffset { get; init; }

    /// <summary>Total chunks known at the start of the batch.</summary>
    public int TotalChunks { get; init; }

    /// <summary>Number of chunks inspected by this batch.</summary>
    public int Scanned { get; init; }

    /// <summary>Number of chunks rewritten to the target key id.</summary>
    public int Reencrypted { get; init; }

    /// <summary>Number of chunks already stored with the target key id.</summary>
    public int AlreadyCurrent { get; init; }

    /// <summary>Number of chunk rows whose storage object was missing.</summary>
    public int Missing { get; init; }

    /// <summary>Number of chunks that failed to rewrite or verify.</summary>
    public int Failed { get; init; }

    /// <summary>Whether the scan reached the end of the current chunk table.</summary>
    public bool Completed { get; init; }
}
