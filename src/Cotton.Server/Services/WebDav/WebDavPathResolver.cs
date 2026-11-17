// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Topology;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.WebDav
{
    public class WebDavPathResolver(
        CottonDbContext _dbContext,
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
            string cleanPath = NormalizeAndValidatePath(path);

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

            string[] parts = cleanPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            string parentPath = string.Join(PathSeparator, parts.Take(parts.Length - 1));
            ResolvedNodePath? parent = await _navigator.ResolveNodePathAsync(userId, parentPath, DefaultNodeType, ct);
            if (parent is null)
            {
                return new WebDavResolveResult { Found = false };
            }

            // Now check the last part - it can be a node or a file
            string lastName = parts[^1];
            string lastNameKey = NameValidator.NormalizeAndGetNameKey(lastName);

            Node currentNode = parent.Node;
            Node? childNode = await _navigator.FindChildNodeAsync(currentNode, lastName, ct);

            if (childNode is not null)
            {
                return new WebDavResolveResult
                {
                    Found = true,
                    IsCollection = true,
                    Path = CombinePath(parent.Path, childNode.Name),
                    Node = childNode
                };
            }

            // Try to find as file
            IQueryable<NodeFile> fileQuery = new NodeDirectory(_dbContext, currentNode).Files
                .Where(x => x.NameKey == lastNameKey);

            if (includeFileContentGraph)
            {
                fileQuery = fileQuery
                    .Include(x => x.Node)
                    .Include(x => x.FileManifest)
                    .ThenInclude(x => x.FileManifestChunks)
                    .ThenInclude(x => x.Chunk);
            }
            else
            {
                fileQuery = fileQuery
                    .AsNoTracking()
                    .Include(x => x.FileManifest);
            }

            NodeFile? nodeFile = await fileQuery.SingleOrDefaultAsync(ct);

            if (nodeFile is not null)
            {
                return new WebDavResolveResult
                {
                    Found = true,
                    IsCollection = false,
                    Path = CombinePath(parent.Path, nodeFile.Name),
                    NodeFile = nodeFile
                };
            }

            return new WebDavResolveResult { Found = false };
        }

        public async Task<WebDavParentResult> GetParentNodeAsync(Guid userId, string path, CancellationToken ct = default)
        {
            string cleanPath = NormalizeAndValidatePath(path);
            (Node Parent, string ResourceName)? resolved = await _navigator.ResolveParentAndNameAsync(userId, cleanPath, DefaultNodeType, ct);
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

        private static string CombinePath(string parentPath, string name) =>
            string.IsNullOrEmpty(parentPath) ? name : parentPath + PathSeparator + name;

        private static string NormalizeAndValidatePath(string? path)
        {
            string p = (path ?? string.Empty).Replace('\\', PathSeparator);
            foreach (string part in p.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!NameValidator.TryNormalizeAndValidate(part, out _, out string error))
                {
                    throw new BadHttpRequestException(error);
                }
            }
            return p.Trim(PathSeparator);
        }
    }
}
