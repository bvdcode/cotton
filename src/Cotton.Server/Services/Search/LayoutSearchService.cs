// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.Search;

/// <summary>
/// Coordinates layout search providers and shapes the API result payload.
/// </summary>
public sealed class LayoutSearchService(
    CottonDbContext _dbContext,
    IEnumerable<ILayoutSearchProvider> _providers) : ILayoutSearchService
{
    private const int MaxPageSize = 100;

    /// <inheritdoc />
    public async Task<SearchLayoutsResultDto> SearchAsync(LayoutSearchRequest request, CancellationToken cancellationToken)
    {
        ValidatePaging(request.Page, request.PageSize);

        LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build(request.Query);
        if (!criteria.HasText && !criteria.HasIds)
        {
            return new SearchLayoutsResultDto();
        }

        IQueryable<LayoutSearchHit>? hitsQuery = BuildHitsQuery(request, criteria);
        if (hitsQuery is null)
        {
            return new SearchLayoutsResultDto();
        }

        hitsQuery = LayoutSearchHitMerger.MergeDuplicateHits(hitsQuery);

        int totalCount = await hitsQuery.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return CreateEmptySearchResult(totalCount);
        }

        int skip = checked((request.Page - 1) * request.PageSize);
        List<LayoutSearchHit> hits = await LoadPagedHitsAsync(hitsQuery, skip, request.PageSize, cancellationToken);
        if (hits.Count == 0)
        {
            return CreateEmptySearchResult(totalCount);
        }

        var (nodes, files) = await LoadHitModelsAsync(hits, cancellationToken);
        var (nodePaths, filePaths) = await ResolvePathsAsync(
            request.UserId,
            request.LayoutId,
            hits,
            cancellationToken);

        return new SearchLayoutsResultDto
        {
            Nodes = nodes,
            Files = files,
            NodePaths = nodePaths,
            FilePaths = filePaths,
            TotalCount = totalCount,
        };
    }

    private IQueryable<LayoutSearchHit>? BuildHitsQuery(LayoutSearchRequest request, LayoutSearchCriteria criteria)
    {
        LayoutSearchProviderContext context = new(request, criteria);
        IQueryable<LayoutSearchHit>? hitsQuery = null;

        foreach (ILayoutSearchProvider provider in _providers.OrderBy(x => x.Priority))
        {
            if (!provider.CanSearch(criteria))
            {
                continue;
            }

            IQueryable<LayoutSearchHit> providerQuery = provider.BuildHitsQuery(context);
            hitsQuery = hitsQuery is null
                ? providerQuery
                : hitsQuery.Concat(providerQuery);
        }

        return hitsQuery;
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"PageSize cannot be greater than {MaxPageSize}.");
        }
    }

    private static SearchLayoutsResultDto CreateEmptySearchResult(int totalCount)
    {
        return new SearchLayoutsResultDto
        {
            TotalCount = totalCount,
        };
    }

    private static async Task<List<LayoutSearchHit>> LoadPagedHitsAsync(
        IQueryable<LayoutSearchHit> hitsQuery,
        int skip,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await hitsQuery
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Kind)
            .ThenBy(x => x.NameKey)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<(List<NodeDto> Nodes, List<NodeFileManifestDto> Files)> LoadHitModelsAsync(
        IReadOnlyList<LayoutSearchHit> hits,
        CancellationToken cancellationToken)
    {
        var nodeIds = hits
            .Where(x => x.Kind == LayoutSearchHitKind.Node)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        var fileIds = hits
            .Where(x => x.Kind == LayoutSearchHitKind.File)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        var nodes = await LoadNodesAsync(nodeIds, cancellationToken);
        var files = await LoadFilesAsync(fileIds, cancellationToken);
        return (OrderNodesLikeHits(nodes, nodeIds), OrderFilesLikeHits(files, fileIds));
    }

    private async Task<List<NodeDto>> LoadNodesAsync(Guid[] nodeIds, CancellationToken cancellationToken)
    {
        if (nodeIds.Length == 0)
        {
            return [];
        }

        return await _dbContext.Nodes
            .AsNoTracking()
            .Where(x => nodeIds.Contains(x.Id))
            .ProjectToType<NodeDto>()
            .ToListAsync(cancellationToken);
    }

    private async Task<List<NodeFileManifestDto>> LoadFilesAsync(Guid[] fileIds, CancellationToken cancellationToken)
    {
        if (fileIds.Length == 0)
        {
            return [];
        }

        return await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => fileIds.Contains(x.Id))
            .Include(x => x.FileManifest)
            .ProjectToType<NodeFileManifestDto>()
            .ToListAsync(cancellationToken);
    }

    private static List<NodeDto> OrderNodesLikeHits(List<NodeDto> nodes, IReadOnlyList<Guid> orderedIds)
    {
        if (nodes.Count <= 1)
        {
            return nodes;
        }

        var order = orderedIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return nodes
            .OrderBy(x => order.GetValueOrDefault(x.Id, int.MaxValue))
            .ToList();
    }

    private static List<NodeFileManifestDto> OrderFilesLikeHits(
        List<NodeFileManifestDto> files,
        IReadOnlyList<Guid> orderedIds)
    {
        if (files.Count <= 1)
        {
            return files;
        }

        var order = orderedIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return files
            .OrderBy(x => order.GetValueOrDefault(x.Id, int.MaxValue))
            .ToList();
    }

    private async Task<(Dictionary<Guid, string> NodePaths, Dictionary<Guid, string> FilePaths)> ResolvePathsAsync(
        Guid userId,
        Guid layoutId,
        IReadOnlyList<LayoutSearchHit> hits,
        CancellationToken cancellationToken)
    {
        var resultNodeIds = hits
            .Where(x => x.Kind == LayoutSearchHitKind.Node)
            .Select(x => x.Id)
            .ToHashSet();

        var fileParentNodeIds = hits
            .Where(x => x.Kind == LayoutSearchHitKind.File)
            .Select(x => x.NodeIdForPath)
            .ToHashSet();

        var allNodeIdsNeededForPaths = resultNodeIds
            .Concat(fileParentNodeIds)
            .ToHashSet();

        if (allNodeIdsNeededForPaths.Count == 0)
        {
            return ([], []);
        }

        var allNodePaths = await ResolveNodePathsAsync(
            userId,
            layoutId,
            allNodeIdsNeededForPaths,
            cancellationToken);

        var nodePaths = new Dictionary<Guid, string>(resultNodeIds.Count);
        foreach (Guid nodeId in resultNodeIds)
        {
            nodePaths[nodeId] = allNodePaths.TryGetValue(nodeId, out string? path)
                ? path
                : Constants.DefaultPathSeparator.ToString();
        }

        var filePaths = new Dictionary<Guid, string>();
        foreach (LayoutSearchHit hit in hits.Where(x => x.Kind == LayoutSearchHitKind.File))
        {
            string parentPath = allNodePaths.TryGetValue(hit.NodeIdForPath, out string? path)
                ? path
                : Constants.DefaultPathSeparator.ToString();

            filePaths[hit.Id] = CombinePath(parentPath, hit.Name);
        }

        return (nodePaths, filePaths);
    }

    private static string CombinePath(string parentPath, string name)
    {
        char separator = Constants.DefaultPathSeparator;

        if (string.IsNullOrWhiteSpace(parentPath))
        {
            parentPath = separator.ToString();
        }

        return parentPath.TrimEnd(separator) + separator + name;
    }

    private async Task<Dictionary<Guid, string>> ResolveNodePathsAsync(
        Guid userId,
        Guid layoutId,
        IEnumerable<Guid> startNodeIds,
        CancellationToken cancellationToken)
    {
        var nodeIds = startNodeIds.ToHashSet();
        if (nodeIds.Count == 0)
        {
            return [];
        }

        var nodeInfo = await LoadNodeLineageAsync(userId, layoutId, nodeIds, cancellationToken);

        Dictionary<Guid, string> nodePaths = new(nodeIds.Count);
        foreach (Guid id in nodeIds)
        {
            nodePaths[id] = ResolveNodePath(nodeInfo, id);
        }

        return nodePaths;
    }

    private async Task<Dictionary<Guid, (Guid? ParentId, string Name, int Type)>> LoadNodeLineageAsync(
        Guid userId,
        Guid layoutId,
        HashSet<Guid> startNodeIds,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, (Guid? ParentId, string Name, int Type)> nodeInfo = [];
        var frontier = new HashSet<Guid>(startNodeIds);

        while (frontier.Count > 0)
        {
            Guid[] ids = [.. frontier];
            frontier.Clear();

            var chunk = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.OwnerId == userId
                    && x.LayoutId == layoutId
                    && ids.Contains(x.Id))
                .Select(x => new { x.Id, x.ParentId, x.Name, x.Type })
                .ToListAsync(cancellationToken);

            foreach (var node in chunk)
            {
                if (nodeInfo.ContainsKey(node.Id))
                {
                    continue;
                }

                nodeInfo[node.Id] = (node.ParentId, node.Name, (int)node.Type);

                if (node.ParentId.HasValue && !nodeInfo.ContainsKey(node.ParentId.Value))
                {
                    frontier.Add(node.ParentId.Value);
                }
            }
        }

        foreach (var (id, info) in nodeInfo.ToArray())
        {
            if (info.ParentId.HasValue
                && nodeInfo.TryGetValue(info.ParentId.Value, out var parent)
                && parent.Type != info.Type)
            {
                nodeInfo[id] = (null, info.Name, info.Type);
            }
        }

        return nodeInfo;
    }

    private static string ResolveNodePath(
        IReadOnlyDictionary<Guid, (Guid? ParentId, string Name, int Type)> nodeInfo,
        Guid id)
    {
        const int MaxDepth = 256;

        var parts = new Stack<string>();
        var visited = new HashSet<Guid>();

        Guid currentId = id;
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

        return Constants.DefaultPathSeparator + string.Join(Constants.DefaultPathSeparator, parts);
    }
}
