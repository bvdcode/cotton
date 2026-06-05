// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Contracts.Nodes;

/// <summary>
/// Represents a create-node request.
/// </summary>
public sealed class CreateNodeRequestDto
{
    /// <summary>
    /// Gets or sets the parent node identifier.
    /// </summary>
    public Guid ParentId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Represents a move-node request.
/// </summary>
public sealed class MoveNodeRequestDto
{
    /// <summary>
    /// Gets or sets the target parent node identifier.
    /// </summary>
    public Guid ParentId { get; set; }
}

/// <summary>
/// Represents a rename-node request.
/// </summary>
public sealed class RenameNodeRequestDto
{
    /// <summary>
    /// Gets or sets the new display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
