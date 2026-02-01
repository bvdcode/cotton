// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Query for WebDAV PROPFIND operation
/// </summary>
public record WebDavPropFindQuery(
    Guid UserId,
    string Path,
    string HrefBase,
    int Depth = 1) : IRequest<WebDavPropFindResult>;

/// <summary>
/// Result of WebDAV PROPFIND operation
/// </summary>
public record WebDavPropFindResult(
    bool Found,
    string? XmlResponse);

/// <summary>
/// Handler for WebDAV PROPFIND operation
/// </summary>
public class WebDavPropFindQueryHandler(
    CottonDbContext _dbContext,
    IWebDavPathResolver _pathResolver,
    ILogger<WebDavPropFindQueryHandler> _logger)
    : IRequestHandler<WebDavPropFindQuery, WebDavPropFindResult>
{

    private const int MaxDepth = 32;

    public async Task<WebDavPropFindResult> Handle(WebDavPropFindQuery request, CancellationToken ct)
    {
        var resolveResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);

        if (!resolveResult.Found)
        {
            _logger.LogDebug("WebDAV PROPFIND: Path not found: {Path}", request.Path);
            return new WebDavPropFindResult(false, null);
        }

        var resources = new List<WebDavResource>();
        var hrefBase = EnsureTrailingSlash(request.HrefBase);
        var depth = Math.Clamp(request.Depth, 0, MaxDepth);
        var pathCache = new Dictionary<Guid, Database.Models.Node>();

        if (resolveResult.IsCollection && resolveResult.Node is not null)
        {
            var node = resolveResult.Node;
            var nodePath = await BuildNodePathAsync(node, pathCache, ct);
            var nodeHref = BuildHref(hrefBase, nodePath);

            // Add the collection itself
            resources.Add(new WebDavResource(
                Href: EnsureTrailingSlash(nodeHref),
                DisplayName: node.Name,
                IsCollection: true,
                ContentLength: 0,
                LastModified: node.UpdatedAt,
                ETag: $"\"{node.Id}\""));

            // If depth > 0, add children
            if (depth > 0)
            {
                await AddChildResourcesAsync(resources, node, hrefBase, nodePath, depth, 1, pathCache, ct);
            }
        }
        else if (resolveResult.NodeFile is not null)
        {
            var nodeFile = resolveResult.NodeFile;
            var parentNode = await _dbContext.Nodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == nodeFile.NodeId, ct);

            var parentPath = parentNode is not null
                ? await BuildNodePathAsync(parentNode, pathCache, ct)
                : string.Empty;

            var fileHref = BuildHref(hrefBase, parentPath, nodeFile.Name);

            resources.Add(new WebDavResource(
                Href: fileHref,
                DisplayName: nodeFile.Name,
                IsCollection: false,
                ContentLength: nodeFile.FileManifest.SizeBytes,
                LastModified: nodeFile.UpdatedAt,
                ETag: $"\"{nodeFile.Id}\"",
                ContentType: nodeFile.FileManifest.ContentType));
        }

        var xml = WebDavXmlBuilder.BuildMultiStatusResponse(resources);
        return new WebDavPropFindResult(true, xml);
    }

    private async Task AddChildResourcesAsync(
        List<WebDavResource> resources,
        Database.Models.Node parentNode,
        string hrefBase,
        string parentPath,
        int maxDepth,
        int currentDepth,
        Dictionary<Guid, Database.Models.Node> pathCache,
        CancellationToken ct)
    {
        // Get child nodes (folders)
        var childNodes = await _dbContext.Nodes
            .AsNoTracking()
            .Where(n => n.ParentId == parentNode.Id
                && n.Type == WebDavPathResolver.DefaultNodeType
                && n.OwnerId == parentNode.OwnerId
                && n.LayoutId == parentNode.LayoutId)
            .OrderBy(n => n.NameKey)
            .ToListAsync(ct);

        foreach (var childNode in childNodes)
        {
            var childPath = string.IsNullOrEmpty(parentPath)
                ? childNode.Name
                : $"{parentPath}{WebDavPathResolver.PathSeparator}{childNode.Name}";

            resources.Add(new WebDavResource(
                Href: EnsureTrailingSlash(BuildHref(hrefBase, childPath)),
                DisplayName: childNode.Name,
                IsCollection: true,
                ContentLength: 0,
                LastModified: childNode.UpdatedAt,
                ETag: $"\"{childNode.Id}\""));

            if (currentDepth < maxDepth)
            {
                await AddChildResourcesAsync(resources, childNode, hrefBase, childPath, maxDepth, currentDepth + 1, pathCache, ct);
            }
        }

        // Get child files
        var childFiles = await _dbContext.NodeFiles
            .AsNoTracking()
            .Include(f => f.FileManifest)
            .Where(f => f.NodeId == parentNode.Id
                && f.OwnerId == parentNode.OwnerId)
            .OrderBy(f => f.NameKey)
            .ToListAsync(ct);

        foreach (var childFile in childFiles)
        {
            var filePath = string.IsNullOrEmpty(parentPath)
                ? childFile.Name
                : $"{parentPath}{WebDavPathResolver.PathSeparator}{childFile.Name}";

            resources.Add(new WebDavResource(
                Href: BuildHref(hrefBase, filePath),
                DisplayName: childFile.Name,
                IsCollection: false,
                ContentLength: childFile.FileManifest.SizeBytes,
                LastModified: childFile.UpdatedAt,
                ETag: $"\"{childFile.Id}\"",
                ContentType: childFile.FileManifest.ContentType));
        }
    }

    private async Task<string> BuildNodePathAsync(
        Database.Models.Node node,
        Dictionary<Guid, Database.Models.Node> cache,
        CancellationToken ct)
    {
        if (!cache.ContainsKey(node.Id))
        {
            cache[node.Id] = node;
        }
        var parts = new List<string>();
        var current = node;

        // Don't include root node in path
        while (current.ParentId is not null)
        {
            parts.Add(current.Name);

            if (!cache.TryGetValue(current.ParentId.Value, out var parent))
            {
                parent = await _dbContext.Nodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == current.ParentId, ct);

                if (parent is not null)
                {
                    cache[parent.Id] = parent;
                }
            }

            current = parent;

            if (current is null)
            {
                break;
            }
        }

        parts.Reverse();
        return string.Join(WebDavPathResolver.PathSeparator, parts);
    }

    private static string BuildHref(string baseHref, params string[] pathParts)
    {
        var path = string.Join(WebDavPathResolver.PathSeparator, pathParts.Where(p => !string.IsNullOrEmpty(p)));
        if (string.IsNullOrEmpty(path))
        {
            return baseHref;
        }
        return baseHref.TrimEnd(WebDavPathResolver.PathSeparator) + WebDavPathResolver.PathSeparator + path;
    }

    private static string EnsureTrailingSlash(string href) =>
        href.EndsWith(WebDavPathResolver.PathSeparator) ? href : href + WebDavPathResolver.PathSeparator;
}
