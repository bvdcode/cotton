// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Represents a sync path collision that cannot be reconciled safely on case-insensitive file systems.
/// </summary>
public sealed class SyncPathCollisionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPathCollisionException" /> class.
    /// </summary>
    public SyncPathCollisionException(string firstPath, string secondPath)
        : base($"Case-insensitive path collision detected between '{firstPath}' and '{secondPath}'.")
    {
        FirstPath = firstPath;
        SecondPath = secondPath;
    }

    /// <summary>
    /// Gets the first colliding relative path.
    /// </summary>
    public string FirstPath { get; }

    /// <summary>
    /// Gets the second colliding relative path.
    /// </summary>
    public string SecondPath { get; }
}
