// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.WebDav;

/// <summary>
/// Resolves WebDAV paths to nodes and files
/// </summary>
public class WebDavPathResolver(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    ILayoutNavigator _navigator) : IWebDavPathResolver
{
    public const NodeType DefaultNodeType = NodeType.Default;
    public const char PathSeparator = Constants.DefaultPathSeparator;

    public Task<WebDavResolveResult> ResolvePathAsync(Guid userId, string path, CancellationToken ct = default)
    {
        return ResolveInternalAsync(userId, path, includeFileContentGraph: true, ct);
    }

    public Task<WebDavResolveResult> ResolveMetadataAsync(Guid userId, string path, CancellationToken ct = default)
    {
        return ResolveInternalAsync(userId, path, includeFileContentGraph: false, ct);
    }

    private async Task<WebDavResolveResult> ResolveInternalAsync(
        Guid userId,
        string path,
        bool includeFileContentGraph,
        CancellationToken ct)
    {
        var cleanPath = NormalizePath(path);

        // Root path
        if (string.IsNullOrEmpty(cleanPath))
        {
            var (_, root) = await _navigator.GetLayoutAndRootAsync(userId, DefaultNodeType, ct);
            return new WebDavResolveResult
            {
                Found = true,
                IsCollection = true,
                Node = root
            };
        }

        var parts = cleanPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(SafeUnescapePathSegment)
            .ToArray();
        var parentPath = string.Join(PathSeparator, parts.Take(parts.Length - 1));
        var currentNode = await _navigator.ResolveNodeByPathAsync(userId, parentPath, DefaultNodeType, ct);
        if (currentNode is null)
        {
            return new WebDavResolveResult { Found = false };
        }

        // Now check the last part - it can be a node or a file
        var lastName = parts[^1];
        var lastNameKey = NameValidator.NormalizeAndGetNameKey(lastName);

        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);

        // Try to find as node first
        var childNode = await _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.LayoutId == layout.Id
                && x.ParentId == currentNode.Id
                && x.OwnerId == userId
                && x.NameKey == lastNameKey
                && x.Type == DefaultNodeType)
            .SingleOrDefaultAsync(ct);

        if (childNode is not null)
        {
            return new WebDavResolveResult
            {
                Found = true,
                IsCollection = true,
                Node = childNode
            };
        }

        // Try to find as file
        var fileQuery = _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.NodeId == currentNode.Id
                && x.OwnerId == userId
                && x.NameKey == lastNameKey);

        if (includeFileContentGraph)
        {
            fileQuery = fileQuery
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk);
        }
        else
        {
            fileQuery = fileQuery
                .Include(x => x.FileManifest);
        }

        var nodeFile = await fileQuery.SingleOrDefaultAsync(ct);

        if (nodeFile is not null)
        {
            return new WebDavResolveResult
            {
                Found = true,
                IsCollection = false,
                NodeFile = nodeFile
            };
        }

        return new WebDavResolveResult { Found = false };
    }

    public async Task<WebDavParentResult> GetParentNodeAsync(Guid userId, string path, CancellationToken ct = default)
    {
        // Decode percent-encoded sequences so Windows WebDAV clients can upload names containing
        // reserved URL characters like '#' and '%' (sent as %23, %25).
        var decodedPath = Uri.UnescapeDataString(path ?? string.Empty);
        var resolved = await _navigator.ResolveParentAndNameAsync(userId, decodedPath, DefaultNodeType, ct);
        if (resolved is null)
        {
            return new WebDavParentResult { Found = false };
        }

        return new WebDavParentResult
        {
            Found = true,
            ParentNode = resolved.Value.Parent,
            ResourceName = resolved.Value.ResourceName
        };
    }

    private static string NormalizePath(string? path)
    {
        var p = (path ?? string.Empty).Replace('\\', PathSeparator);
        return p.Trim(PathSeparator);
    }

    private static string SafeUnescapePathSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return string.Empty;
        }

        try
        {
            return Uri.UnescapeDataString(segment);
        }
        catch (UriFormatException)
        {
            // Some clients may send raw '%' (not percent-encoded) or otherwise malformed escapes.
            // Treat it as a literal segment to avoid failing the whole request.
            return segment;
        }
    }
}
