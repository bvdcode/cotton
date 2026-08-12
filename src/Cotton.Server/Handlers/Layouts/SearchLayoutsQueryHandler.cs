// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.Search;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Layouts
{
    public class SearchLayoutsQueryHandler(
        CottonDbContext _dbContext,
        IEnumerable<ILayoutSearchProvider> _providers)
        : IRequestHandler<SearchLayoutsQuery, PagedResult<SearchResultDto>>
    {
        private const int MaxPageSize = 100;

        public async Task<PagedResult<SearchResultDto>> Handle(
            SearchLayoutsQuery request,
            CancellationToken cancellationToken)
        {
            ValidatePaging(request.Page, request.PageSize);

            LayoutSearchRequest searchRequest = new(
                request.UserId,
                request.LayoutId,
                request.Query,
                request.Page,
                request.PageSize);
            LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build(request.Query);
            if (!criteria.HasText && !criteria.HasIds)
            {
                return CreateEmptySearchResult(0);
            }

            IQueryable<LayoutSearchHit>? hitsQuery = BuildHitsQuery(searchRequest, criteria);
            if (hitsQuery is null)
            {
                return CreateEmptySearchResult(0);
            }

            hitsQuery = LayoutSearchHitMerger.MergeDuplicateHits(hitsQuery);

            int totalCount = await hitsQuery.CountAsync(cancellationToken);
            if (totalCount == 0)
            {
                return CreateEmptySearchResult(totalCount);
            }

            int skip = checked((request.Page - 1) * request.PageSize);
            List<LayoutSearchHit> hits = await LoadPagedHitsAsync(
                hitsQuery,
                skip,
                request.PageSize,
                cancellationToken);
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

            return new(new SearchResultDto
            {
                Nodes = nodes,
                Files = files,
                NodePaths = nodePaths,
                FilePaths = filePaths,
            }, totalCount);
        }

        private IQueryable<LayoutSearchHit>? BuildHitsQuery(
            LayoutSearchRequest request,
            LayoutSearchCriteria criteria)
        {
            LayoutSearchProviderContext context = new(request, criteria);
            IQueryable<LayoutSearchHit>? hitsQuery = null;

            foreach (ILayoutSearchProvider provider in _providers.OrderBy(provider => provider.Priority))
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

        private static PagedResult<SearchResultDto> CreateEmptySearchResult(int totalCount)
        {
            return new(new SearchResultDto(), totalCount);
        }

        private static async Task<List<LayoutSearchHit>> LoadPagedHitsAsync(
            IQueryable<LayoutSearchHit> hitsQuery,
            int skip,
            int pageSize,
            CancellationToken cancellationToken)
        {
            return await hitsQuery
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => hit.Kind)
                .ThenBy(hit => hit.NameKey)
                .ThenBy(hit => hit.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        private async Task<(List<NodeDto> Nodes, List<NodeFileManifestDto> Files)> LoadHitModelsAsync(
            IReadOnlyList<LayoutSearchHit> hits,
            CancellationToken cancellationToken)
        {
            Guid[] nodeIds = hits
                .Where(hit => hit.Kind == LayoutSearchHitKind.Node)
                .Select(hit => hit.Id)
                .Distinct()
                .ToArray();

            Guid[] fileIds = hits
                .Where(hit => hit.Kind == LayoutSearchHitKind.File)
                .Select(hit => hit.Id)
                .Distinct()
                .ToArray();

            List<NodeDto> nodes = await LoadNodesAsync(nodeIds, cancellationToken);
            List<NodeFileManifestDto> files = await LoadFilesAsync(fileIds, cancellationToken);
            return (OrderNodesLikeHits(nodes, nodeIds), OrderFilesLikeHits(files, fileIds));
        }

        private async Task<List<NodeDto>> LoadNodesAsync(
            Guid[] nodeIds,
            CancellationToken cancellationToken)
        {
            if (nodeIds.Length == 0)
            {
                return [];
            }

            return await _dbContext.Nodes
                .AsNoTracking()
                .Where(node => nodeIds.Contains(node.Id))
                .ProjectToType<NodeDto>()
                .ToListAsync(cancellationToken);
        }

        private async Task<List<NodeFileManifestDto>> LoadFilesAsync(
            Guid[] fileIds,
            CancellationToken cancellationToken)
        {
            if (fileIds.Length == 0)
            {
                return [];
            }

            return await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(nodeFile => fileIds.Contains(nodeFile.Id))
                .Include(nodeFile => nodeFile.FileManifest)
                .ProjectToType<NodeFileManifestDto>()
                .ToListAsync(cancellationToken);
        }

        private static List<NodeDto> OrderNodesLikeHits(
            List<NodeDto> nodes,
            IReadOnlyList<Guid> orderedIds)
        {
            if (nodes.Count <= 1)
            {
                return nodes;
            }

            var order = orderedIds
                .Select((id, index) => new { id, index })
                .ToDictionary(item => item.id, item => item.index);

            return nodes
                .OrderBy(node => order.GetValueOrDefault(node.Id, int.MaxValue))
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
                .ToDictionary(item => item.id, item => item.index);

            return files
                .OrderBy(file => order.GetValueOrDefault(file.Id, int.MaxValue))
                .ToList();
        }

        private async Task<(Dictionary<Guid, string> NodePaths, Dictionary<Guid, string> FilePaths)> ResolvePathsAsync(
            Guid userId,
            Guid layoutId,
            IReadOnlyList<LayoutSearchHit> hits,
            CancellationToken cancellationToken)
        {
            var resultNodeIds = hits
                .Where(hit => hit.Kind == LayoutSearchHitKind.Node)
                .Select(hit => hit.Id)
                .ToHashSet();

            var fileParentNodeIds = hits
                .Where(hit => hit.Kind == LayoutSearchHitKind.File)
                .Select(hit => hit.NodeIdForPath)
                .ToHashSet();

            var allNodeIdsNeededForPaths = resultNodeIds
                .Concat(fileParentNodeIds)
                .ToHashSet();

            if (allNodeIdsNeededForPaths.Count == 0)
            {
                return ([], []);
            }

            Dictionary<Guid, string> allNodePaths = await ResolveNodePathsAsync(
                userId,
                layoutId,
                allNodeIdsNeededForPaths,
                cancellationToken);

            Dictionary<Guid, string> nodePaths = new(resultNodeIds.Count);
            foreach (Guid nodeId in resultNodeIds)
            {
                nodePaths[nodeId] = allNodePaths.TryGetValue(nodeId, out string? path)
                    ? path
                    : Constants.DefaultPathSeparator.ToString();
            }

            Dictionary<Guid, string> filePaths = [];
            foreach (LayoutSearchHit hit in hits.Where(hit => hit.Kind == LayoutSearchHitKind.File))
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
            HashSet<Guid> nodeIds = startNodeIds.ToHashSet();
            if (nodeIds.Count == 0)
            {
                return [];
            }

            Dictionary<Guid, (Guid? ParentId, string Name, int Type)> nodeInfo = await LoadNodeLineageAsync(
                userId,
                layoutId,
                nodeIds,
                cancellationToken);

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
            HashSet<Guid> frontier = new(startNodeIds);

            while (frontier.Count > 0)
            {
                Guid[] ids = [.. frontier];
                frontier.Clear();

                var chunk = await _dbContext.Nodes
                    .AsNoTracking()
                    .Where(node => node.OwnerId == userId
                        && node.LayoutId == layoutId
                        && ids.Contains(node.Id))
                    .Select(node => new { node.Id, node.ParentId, node.Name, node.Type })
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

            foreach ((Guid id, (Guid? ParentId, string Name, int Type) info) in nodeInfo.ToArray())
            {
                if (info.ParentId.HasValue
                    && nodeInfo.TryGetValue(
                        info.ParentId.Value,
                        out (Guid? ParentId, string Name, int Type) parent)
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

            Stack<string> parts = new();
            HashSet<Guid> visited = [];

            Guid currentId = id;
            int depth = 0;

            while (nodeInfo.TryGetValue(currentId, out (Guid? ParentId, string Name, int Type) info))
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
}
