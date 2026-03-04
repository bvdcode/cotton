// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Public endpoints for browsing shared folders.
    /// No authentication required — access is controlled by the share token.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Base + "/shared/folders")]
    public class SharedFolderController(
        CottonDbContext _dbContext,
        IStoragePipeline _storage) : ControllerBase
    {
        private const int MaxAncestorDepth = 256;

        /// <summary>
        /// Get information about a shared folder.
        /// </summary>
        [HttpGet("{token}")]
        public async Task<IActionResult> GetSharedFolderInfo([FromRoute] string token)
        {
            var shareToken = await FindValidShareTokenAsync(token);
            if (shareToken is null)
            {
                return CottonResult.NotFound("Shared folder not found or link expired.");
            }

            return Ok(new SharedFolderInfoDto
            {
                Name = shareToken.Name,
                NodeId = shareToken.NodeId,
                CreatedAt = shareToken.CreatedAt,
                ExpiresAt = shareToken.ExpiresAt,
            });
        }

        /// <summary>
        /// Get the children of the shared folder root.
        /// </summary>
        [HttpGet("{token}/children")]
        public async Task<IActionResult> GetSharedFolderChildren(
            [FromRoute] string token,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            var shareToken = await FindValidShareTokenAsync(token);
            if (shareToken is null)
            {
                return CottonResult.NotFound("Shared folder not found or link expired.");
            }

            return await GetNodeChildrenAsync(shareToken, shareToken.NodeId, page, pageSize);
        }

        /// <summary>
        /// Get the children of a subfolder within the shared folder tree.
        /// Validates that the requested node is a descendant of the shared root.
        /// </summary>
        [HttpGet("{token}/nodes/{nodeId:guid}/children")]
        public async Task<IActionResult> GetSharedSubfolderChildren(
            [FromRoute] string token,
            [FromRoute] Guid nodeId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            var shareToken = await FindValidShareTokenAsync(token);
            if (shareToken is null)
            {
                return CottonResult.NotFound("Shared folder not found or link expired.");
            }

            if (nodeId == shareToken.NodeId)
            {
                return await GetNodeChildrenAsync(shareToken, nodeId, page, pageSize);
            }

            bool isDescendant = await IsDescendantOfAsync(nodeId, shareToken.NodeId, shareToken.Node.OwnerId);
            if (!isDescendant)
            {
                return CottonResult.NotFound("Folder not found within the shared folder.");
            }

            return await GetNodeChildrenAsync(shareToken, nodeId, page, pageSize);
        }

        /// <summary>
        /// Get the ancestor chain of a node within the shared folder, stopping at the shared root.
        /// Used for breadcrumb navigation in the shared folder view.
        /// </summary>
        [HttpGet("{token}/nodes/{nodeId:guid}/ancestors")]
        public async Task<IActionResult> GetSharedNodeAncestors(
            [FromRoute] string token,
            [FromRoute] Guid nodeId)
        {
            var shareToken = await FindValidShareTokenAsync(token);
            if (shareToken is null)
            {
                return CottonResult.NotFound("Shared folder not found or link expired.");
            }

            if (nodeId == shareToken.NodeId)
            {
                return Ok(Array.Empty<NodeDto>());
            }

            var ancestors = await BuildAncestorsUpToRootAsync(
                nodeId, shareToken.NodeId, shareToken.Node.OwnerId);
            if (ancestors is null)
            {
                return CottonResult.NotFound("Node not found within the shared folder.");
            }

            return Ok(ancestors);
        }

        /// <summary>
        /// Download a file from within the shared folder tree.
        /// </summary>
        [HttpGet("{token}/files/{nodeFileId:guid}/download")]
        public async Task<IActionResult> DownloadSharedFolderFile(
            [FromRoute] string token,
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool inline = false)
        {
            var shareToken = await FindValidShareTokenAsync(token);
            if (shareToken is null)
            {
                return CottonResult.NotFound("Shared folder not found or link expired.");
            }

            var nodeFile = await _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .Where(x => x.Id == nodeFileId && x.Node.Type == NodeType.Default)
                .SingleOrDefaultAsync();

            if (nodeFile is null)
            {
                return CottonResult.NotFound("File not found.");
            }

            bool isInSharedTree = nodeFile.NodeId == shareToken.NodeId
                || await IsDescendantOfAsync(nodeFile.NodeId, shareToken.NodeId, shareToken.Node.OwnerId);

            if (!isInSharedTree)
            {
                return CottonResult.NotFound("File not found within the shared folder.");
            }

            string[] chunkHashes = nodeFile.FileManifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = nodeFile.FileManifest.SizeBytes,
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.GetChunkLengths(),
            };

            Stream stream = _storage.GetBlobStream(chunkHashes, context);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";

            var entityTag = EntityTagHeaderValue.Parse(
                $"\"sha256-{Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash)}\"");
            var lastModified = new DateTimeOffset(nodeFile.CreatedAt);

            return File(
                stream,
                nodeFile.FileManifest.ContentType,
                fileDownloadName: inline ? null : nodeFile.Name,
                lastModified: lastModified,
                entityTag: entityTag,
                enableRangeProcessing: true);
        }

        /// <summary>
        /// Get file metadata for inline preview (HEAD-like info via GET).
        /// </summary>
        [HttpHead("{token}/files/{nodeFileId:guid}/download")]
        public async Task<IActionResult> HeadSharedFolderFile(
            [FromRoute] string token,
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool inline = false)
        {
            var shareToken = await FindValidShareTokenAsync(token);
            if (shareToken is null)
            {
                return CottonResult.NotFound("Shared folder not found or link expired.");
            }

            var nodeFile = await _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.Node.Type == NodeType.Default)
                .SingleOrDefaultAsync();

            if (nodeFile is null)
            {
                return CottonResult.NotFound("File not found.");
            }

            bool isInSharedTree = nodeFile.NodeId == shareToken.NodeId
                || await IsDescendantOfAsync(nodeFile.NodeId, shareToken.NodeId, shareToken.Node.OwnerId);

            if (!isInSharedTree)
            {
                return CottonResult.NotFound("File not found within the shared folder.");
            }

            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            Response.ContentType = nodeFile.FileManifest.ContentType;
            Response.ContentLength = nodeFile.FileManifest.SizeBytes;

            var cd = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
            {
                FileNameStar = nodeFile.Name,
                FileName = nodeFile.Name,
            };
            Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
            return new EmptyResult();
        }

        private async Task<NodeShareToken?> FindValidShareTokenAsync(string token)
        {
            DateTime now = DateTime.UtcNow;
            return await _dbContext.NodeShareTokens
                .AsNoTracking()
                .Where(x => x.Token == token
                    && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .Include(x => x.Node)
                .FirstOrDefaultAsync();
        }

        private async Task<IActionResult> GetNodeChildrenAsync(
            NodeShareToken shareToken, Guid parentNodeId, int page, int pageSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

            Guid ownerId = shareToken.Node.OwnerId;
            Guid layoutId = shareToken.Node.LayoutId;
            int skip = (page - 1) * pageSize;

            IQueryable<NodeDto> nodesQuery = _dbContext.Nodes
                .AsNoTracking()
                .OrderBy(x => x.NameKey)
                .Where(x => x.ParentId == parentNodeId
                    && x.OwnerId == ownerId
                    && x.LayoutId == layoutId
                    && x.Type == NodeType.Default)
                .ProjectToType<NodeDto>();

            var filesBaseQuery = _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.NodeId == parentNodeId);

            int nodesCount = await nodesQuery.CountAsync();
            int filesCount = await filesBaseQuery.CountAsync();
            int totalCount = nodesCount + filesCount;

            int nodesToTake = Math.Max(0, Math.Min(pageSize, nodesCount - skip));
            int filesSkip = Math.Max(0, skip - nodesCount);
            int filesToTake = Math.Max(0, pageSize - nodesToTake);

            var nodes = nodesToTake == 0 ? []
                : await nodesQuery.Skip(skip).Take(nodesToTake).ToListAsync();

            var files = filesToTake == 0 ? []
                : await filesBaseQuery
                    .OrderBy(x => x.NameKey)
                    .Include(x => x.FileManifest)
                    .Skip(filesSkip)
                    .Take(filesToTake)
                    .ProjectToType<NodeFileManifestDto>()
                    .ToListAsync();

            Response.Headers.Append("X-Total-Count", totalCount.ToString());

            return Ok(new NodeContentDto
            {
                Id = parentNodeId,
                Nodes = nodes,
                Files = files,
                TotalCount = totalCount,
            });
        }

        /// <summary>
        /// Checks whether <paramref name="nodeId"/> is a descendant of <paramref name="ancestorNodeId"/>
        /// by walking up the parent chain.
        /// </summary>
        private async Task<bool> IsDescendantOfAsync(Guid nodeId, Guid ancestorNodeId, Guid ownerId)
        {
            var currentId = nodeId;
            var visited = new HashSet<Guid> { currentId };
            int depth = 0;

            while (depth++ < MaxAncestorDepth)
            {
                var parentId = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == currentId && x.OwnerId == ownerId)
                    .Select(x => x.ParentId)
                    .FirstOrDefaultAsync();

                if (!parentId.HasValue)
                {
                    return false;
                }

                if (parentId.Value == ancestorNodeId)
                {
                    return true;
                }

                if (!visited.Add(parentId.Value))
                {
                    return false;
                }

                currentId = parentId.Value;
            }

            return false;
        }

        /// <summary>
        /// Builds the ancestor chain from <paramref name="nodeId"/> up to (but not including)
        /// <paramref name="rootNodeId"/>. Returns null if the node is not a descendant of the root.
        /// </summary>
        private async Task<List<NodeDto>?> BuildAncestorsUpToRootAsync(
            Guid nodeId, Guid rootNodeId, Guid ownerId)
        {
            var ancestors = new List<NodeDto>();
            var currentId = nodeId;
            var visited = new HashSet<Guid> { currentId };
            int depth = 0;

            while (depth++ < MaxAncestorDepth)
            {
                var node = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == currentId && x.OwnerId == ownerId)
                    .FirstOrDefaultAsync();

                if (node is null)
                {
                    return null;
                }

                if (!node.ParentId.HasValue)
                {
                    return null;
                }

                if (node.ParentId.Value == rootNodeId)
                {
                    ancestors.Reverse();
                    return ancestors;
                }

                var parent = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(x => x.Id == node.ParentId.Value && x.OwnerId == ownerId)
                    .FirstOrDefaultAsync();

                if (parent is null)
                {
                    return null;
                }

                ancestors.Add(parent.Adapt<NodeDto>());

                if (!visited.Add(parent.Id))
                {
                    return null;
                }

                currentId = parent.Id;
            }

            return null;
        }
    }
}
