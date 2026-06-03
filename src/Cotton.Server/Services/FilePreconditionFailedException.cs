// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services;

/// <summary>
/// Represents a failed file mutation precondition such as a stale ETag.
/// </summary>
public sealed class FilePreconditionFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilePreconditionFailedException" /> class.
    /// </summary>
    public FilePreconditionFailedException(string message)
        : base(message)
    {
    }
}
