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
    ILayoutService _layouts) : IWebDavPathResolver
{
    public const NodeType DefaultNodeType = NodeType.Default;
    public const char PathSeparator = '/';

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

        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
        var rootNode = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, DefaultNodeType);

        // Root path
        if (string.IsNullOrEmpty(cleanPath))
        {
            return new WebDavResolveResult
            {
                Found = true,
                IsCollection = true,
                Node = rootNode
            };
        }

        var parts = cleanPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var currentNode = rootNode;

        // Navigate to parent node (all parts except the last)
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var nameKey = NameValidator.NormalizeAndGetNameKey(part);

            var nextNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.LayoutId == layout.Id
                    && x.ParentId == currentNode.Id
                    && x.OwnerId == userId
                    && x.NameKey == nameKey
                    && x.Type == DefaultNodeType)
                .SingleOrDefaultAsync(ct);

            if (nextNode is null)
            {
                return new WebDavResolveResult { Found = false };
            }

            currentNode = nextNode;
        }

        // Now check the last part - it can be a node or a file
        var lastName = parts[^1];
        var lastNameKey = NameValidator.NormalizeAndGetNameKey(lastName);

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
        var cleanPath = NormalizePath(path);

        if (string.IsNullOrEmpty(cleanPath))
        {
            return new WebDavParentResult { Found = false };
        }

        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
        var rootNode = await _layouts.GetOrCreateRootNodeAsync(layout.Id, userId, DefaultNodeType);

        var parts = cleanPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var resourceName = parts[^1];

        if (parts.Length == 1)
        {
            return new WebDavParentResult
            {
                Found = true,
                ParentNode = rootNode,
                ResourceName = resourceName
            };
        }

        var currentNode = rootNode;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var nameKey = NameValidator.NormalizeAndGetNameKey(part);

            var nextNode = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.LayoutId == layout.Id
                    && x.ParentId == currentNode.Id
                    && x.OwnerId == userId
                    && x.NameKey == nameKey
                    && x.Type == DefaultNodeType)
                .SingleOrDefaultAsync(ct);

            if (nextNode is null)
            {
                return new WebDavParentResult { Found = false };
            }

            currentNode = nextNode;
        }

        return new WebDavParentResult
        {
            Found = true,
            ParentNode = currentNode,
            ResourceName = resourceName
        };
    }

    private static string NormalizePath(string? path)
    {
        var p = (path ?? string.Empty).Replace('\\', PathSeparator);
        return p.Trim(PathSeparator);
    }
}
