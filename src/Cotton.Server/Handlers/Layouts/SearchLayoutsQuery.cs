// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts;

public class SearchLayoutsQuery(
    Guid userId,
    Guid layoutId,
    string query,
    int page,
    int pageSize) : IRequest<SearchLayoutsResultDto>
{
    public Guid UserId { get; } = userId;
    public Guid LayoutId { get; } = layoutId;
    public string Query { get; } = query;
    public int Page { get; } = page;
    public int PageSize { get; } = pageSize;
}

public class SearchLayoutsQueryHandler(CottonDbContext _dbContext)
    : IRequestHandler<SearchLayoutsQuery, SearchLayoutsResultDto>
{
    public async Task<SearchLayoutsResultDto> Handle(SearchLayoutsQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new BadHttpRequestException("Query cannot be empty.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.PageSize);

        string searchKey = NameValidator.NormalizeAndGetNameKey(request.Query);

        var nodesQuery = _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.OwnerId == request.UserId
                && x.LayoutId == request.LayoutId
                && x.NameKey.Contains(searchKey))
            .OrderBy(x => x.NameKey);

        var filesQuery = _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == request.UserId
                && x.Node.LayoutId == request.LayoutId
                && x.NameKey.Contains(searchKey))
            .OrderBy(x => x.NameKey);

        int nodesCount = await nodesQuery.CountAsync(ct);
        int filesCount = await filesQuery.CountAsync(ct);
        int totalCount = nodesCount + filesCount;

        int skip = (request.Page - 1) * request.PageSize;
        int nodesToTake = Math.Max(0, Math.Min(request.PageSize, nodesCount - skip));
        int filesSkip = Math.Max(0, skip - nodesCount);
        int filesToTake = Math.Max(0, request.PageSize - nodesToTake);

        var nodes = nodesToTake == 0 ? []
            : await nodesQuery.Skip(skip).Take(nodesToTake)
                .ProjectToType<NodeDto>()
                .ToListAsync(ct);

        var files = filesToTake == 0 ? []
            : await filesQuery.Skip(filesSkip).Take(filesToTake)
                .ProjectToType<FileManifestDto>()
                .ToListAsync(ct);

        var nodePaths = await ResolveNodePathsAsync(request.UserId, request.LayoutId, nodes.Select(x => x.Id), ct);

        var filePaths = filesToTake == 0
            ? []
            : await ResolveFilePathsAsync(
                request.UserId,
                request.LayoutId,
                searchKey,
                filesSkip,
                filesToTake,
                nodePaths,
                ct);

        return new SearchLayoutsResultDto
        {
            Nodes = nodes,
            Files = files,
            NodePaths = nodePaths,
            FilePaths = filePaths,
            TotalCount = totalCount,
        };
    }

    private async Task<Dictionary<Guid, string>> ResolveNodePathsAsync(
        Guid userId,
        Guid layoutId,
        IEnumerable<Guid> startNodeIds,
        CancellationToken ct)
    {
        var nodeIds = startNodeIds.ToHashSet();
        if (nodeIds.Count == 0)
        {
            return [];
        }

        var nodeInfo = await LoadNodeLineageAsync(userId, layoutId, nodeIds, ct);

        Dictionary<Guid, string> nodePaths = new(nodeIds.Count);
        foreach (var id in nodeIds)
        {
            nodePaths[id] = ResolveNodePath(nodeInfo, id);
        }

        return nodePaths;
    }

    private async Task<Dictionary<Guid, string>> ResolveFilePathsAsync(
        Guid userId,
        Guid layoutId,
        string searchKey,
        int filesSkip,
        int filesToTake,
        IReadOnlyDictionary<Guid, string> alreadyResolvedNodePaths,
        CancellationToken ct)
    {
        var fileInfos = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == userId
                && x.Node.LayoutId == layoutId
                && x.NameKey.Contains(searchKey))
            .OrderBy(x => x.NameKey)
            .Skip(filesSkip)
            .Take(filesToTake)
            .Select(x => new { x.FileManifestId, x.NodeId, x.Name })
            .ToListAsync(ct);

        var neededNodeIds = fileInfos.Select(x => x.NodeId).ToHashSet();

        Dictionary<Guid, string> fullNodePaths = [];
        foreach (var kvp in alreadyResolvedNodePaths)
        {
            fullNodePaths[kvp.Key] = kvp.Value;
        }

        var missingNodeIds = neededNodeIds.Where(id => !fullNodePaths.ContainsKey(id)).ToHashSet();
        if (missingNodeIds.Count > 0)
        {
            var missingNodeInfo = await LoadNodeLineageAsync(userId, layoutId, missingNodeIds, ct);
            foreach (var id in missingNodeIds)
            {
                fullNodePaths[id] = ResolveNodePath(missingNodeInfo, id);
            }
        }

        var filePaths = new Dictionary<Guid, string>(fileInfos.Count);
        foreach (var f in fileInfos)
        {
            var parentPath = fullNodePaths.TryGetValue(f.NodeId, out var p) ? p : "/";
            filePaths[f.FileManifestId] = parentPath.TrimEnd('/') + "/" + f.Name;
        }

        return filePaths;
    }

    private async Task<Dictionary<Guid, (Guid? ParentId, string Name)>> LoadNodeLineageAsync(
        Guid userId,
        Guid layoutId,
        HashSet<Guid> startNodeIds,
        CancellationToken ct)
    {
        Dictionary<Guid, (Guid? ParentId, string Name)> nodeInfo = [];

        var frontier = new HashSet<Guid>(startNodeIds);
        while (frontier.Count > 0)
        {
            var ids = frontier.ToArray();
            frontier.Clear();

            var chunk = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.OwnerId == userId
                    && x.LayoutId == layoutId
                    && ids.Contains(x.Id))
                .Select(x => new { x.Id, x.ParentId, x.Name })
                .ToListAsync(ct);

            foreach (var n in chunk)
            {
                if (nodeInfo.ContainsKey(n.Id))
                {
                    continue;
                }

                nodeInfo[n.Id] = (n.ParentId, n.Name);

                if (n.ParentId.HasValue && !nodeInfo.ContainsKey(n.ParentId.Value))
                {
                    frontier.Add(n.ParentId.Value);
                }
            }
        }

        return nodeInfo;
    }

    private static string ResolveNodePath(
        IReadOnlyDictionary<Guid, (Guid? ParentId, string Name)> nodeInfo,
        Guid id)
    {
        const int MaxDepth = 256;

        var parts = new Stack<string>();
        var visited = new HashSet<Guid>();
        var currentId = id;
        int depth = 0;

        while (nodeInfo.TryGetValue(currentId, out var info))
        {
            if (!visited.Add(currentId) || depth++ >= MaxDepth)
            {
                break;
            }

            parts.Push(info.Name);
            if (!info.ParentId.HasValue)
            {
                break;
            }
            currentId = info.ParentId.Value;
        }

        return "/" + string.Join('/', parts);
    }
}
