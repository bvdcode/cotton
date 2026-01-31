// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.WebDav;

/// <summary>
/// Resolves WebDAV paths to nodes and files
/// </summary>
public interface IWebDavPathResolver
{
    /// <summary>
    /// Resolves a WebDAV path to a node or file
    /// </summary>
    Task<WebDavResolveResult> ResolvePathAsync(Guid userId, string path, CancellationToken ct = default);

    /// <summary>
    /// Gets the parent node for a given path (for creating new resources)
    /// </summary>
    Task<WebDavParentResult> GetParentNodeAsync(Guid userId, string path, CancellationToken ct = default);
}

/// <summary>
/// Result of resolving a WebDAV path
/// </summary>
public record WebDavResolveResult
{
    public bool Found { get; init; }
    public bool IsCollection { get; init; }
    public Node? Node { get; init; }
    public NodeFile? NodeFile { get; init; }
}

/// <summary>
/// Result of getting parent node for a path
/// </summary>
public record WebDavParentResult
{
    public bool Found { get; init; }
    public Node? ParentNode { get; init; }
    public string? ResourceName { get; init; }
}
