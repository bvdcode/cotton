// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

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
    private const int HitKindNode = 0;
    private const int HitKindFile = 1;

    private const int MaxPageSize = 100;
    private const string LikeEscape = "\\";

    private static readonly Regex GuidRegex = new(
        @"(?<![0-9a-fA-F])(?:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32})(?![0-9a-fA-F])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<SearchLayoutsResultDto> Handle(SearchLayoutsQuery request, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.PageSize);

        if (request.PageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageSize),
                $"PageSize cannot be greater than {MaxPageSize}.");
        }

        var criteria = BuildSearchCriteria(request.Query);

        if (!criteria.HasText && !criteria.HasIds)
        {
            return new SearchLayoutsResultDto();
        }

        int skip = checked((request.Page - 1) * request.PageSize);

        var hasText = criteria.HasText;
        var hasIds = criteria.HasIds;
        var idQueries = criteria.IdQueries;
        var nameKey = criteria.NameKey;
        var containsPattern = criteria.ContainsPattern;
        var prefixPattern = criteria.PrefixPattern;

        var nodesBase = _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.OwnerId == request.UserId
                && x.LayoutId == request.LayoutId);

        if (hasIds && hasText)
        {
            nodesBase = nodesBase.Where(x =>
                idQueries.Contains(x.Id)
                || EF.Functions.Like(x.NameKey, containsPattern, LikeEscape));
        }
        else if (hasIds)
        {
            nodesBase = nodesBase.Where(x => idQueries.Contains(x.Id));
        }
        else
        {
            nodesBase = nodesBase.Where(x =>
                EF.Functions.Like(x.NameKey, containsPattern, LikeEscape));
        }

        var filesBase = _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == request.UserId
                && x.Node.LayoutId == request.LayoutId);

        if (hasIds && hasText)
        {
            filesBase = filesBase.Where(x =>
                idQueries.Contains(x.Id)
                || idQueries.Contains(x.FileManifestId)
                || EF.Functions.Like(x.NameKey, containsPattern, LikeEscape));
        }
        else if (hasIds)
        {
            filesBase = filesBase.Where(x =>
                idQueries.Contains(x.Id)
                || idQueries.Contains(x.FileManifestId));
        }
        else
        {
            filesBase = filesBase.Where(x =>
                EF.Functions.Like(x.NameKey, containsPattern, LikeEscape));
        }

        var nodeHits = nodesBase.Select(x => new SearchHitRow
        {
            Kind = HitKindNode,
            Id = x.Id,
            NodeIdForPath = x.Id,
            Name = x.Name,
            NameKey = x.NameKey,

            // Exact id > exact name > prefix > substring.
            Score =
                hasIds && idQueries.Contains(x.Id) ? 100 :
                hasText && x.NameKey == nameKey ? 80 :
                hasText && EF.Functions.Like(x.NameKey, prefixPattern, LikeEscape) ? 60 :
                20
        });

        var fileHits = filesBase.Select(x => new SearchHitRow
        {
            Kind = HitKindFile,
            Id = x.Id,
            NodeIdForPath = x.NodeId,
            Name = x.Name,
            NameKey = x.NameKey,

            // File.Id чуть важнее FileManifestId, но оба выше текстовых совпадений.
            Score =
                hasIds && idQueries.Contains(x.Id) ? 100 :
                hasIds && idQueries.Contains(x.FileManifestId) ? 95 :
                hasText && x.NameKey == nameKey ? 80 :
                hasText && EF.Functions.Like(x.NameKey, prefixPattern, LikeEscape) ? 60 :
                20
        });

        var hitsQuery = nodeHits.Concat(fileHits);

        int totalCount = await hitsQuery.CountAsync(ct);

        if (totalCount == 0)
        {
            return new SearchLayoutsResultDto
            {
                TotalCount = 0
            };
        }

        var hits = await hitsQuery
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Kind)       // при равном score папки выше файлов
            .ThenBy(x => x.NameKey)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        if (hits.Count == 0)
        {
            return new SearchLayoutsResultDto
            {
                TotalCount = totalCount
            };
        }

        var nodeIds = hits
            .Where(x => x.Kind == HitKindNode)
            .Select(x => x.Id)
            .ToArray();

        var fileIds = hits
            .Where(x => x.Kind == HitKindFile)
            .Select(x => x.Id)
            .ToArray();

        var nodes = nodeIds.Length == 0
            ? []
            : await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => nodeIds.Contains(x.Id))
                .ProjectToType<NodeDto>()
                .ToListAsync(ct);

        var files = fileIds.Length == 0
            ? []
            : await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => fileIds.Contains(x.Id))
                .Include(x => x.FileManifest)
                .ProjectToType<NodeFileManifestDto>()
                .ToListAsync(ct);

        nodes = OrderNodesLikeHits(nodes, nodeIds);
        files = OrderFilesLikeHits(files, fileIds);

        var (nodePaths, filePaths) = await ResolvePathsAsync(
            request.UserId,
            request.LayoutId,
            hits,
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

    private static SearchCriteria BuildSearchCriteria(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new BadHttpRequestException("Query cannot be empty.");
        }

        string rawQuery = query.Normalize(NormalizationForm.FormC).Trim();

        var ids = ExtractGuids(rawQuery)
            .Select(x => (Guid?)x)
            .Distinct()
            .ToArray();

        // Если пользователь вставил URL/лог/строку с GUID внутри,
        // GUID уйдет в id-поиск, а оставшийся текст — в поиск по имени.
        string textWithoutGuids = GuidRegex.Replace(rawQuery, " ");
        string nameKey = NameValidator.GetNameKey(textWithoutGuids);

        string escapedNameKey = EscapeLike(nameKey);

        return new SearchCriteria(
            NameKey: nameKey,
            ContainsPattern: nameKey.Length == 0 ? string.Empty : $"%{escapedNameKey}%",
            PrefixPattern: nameKey.Length == 0 ? string.Empty : $"{escapedNameKey}%",
            IdQueries: ids);
    }

    private static Guid[] ExtractGuids(string value)
    {
        List<Guid> result = [];

        foreach (Match match in GuidRegex.Matches(value))
        {
            if (Guid.TryParse(match.Value, out var guid) && !result.Contains(guid))
            {
                result.Add(guid);
            }
        }

        // На случай формата, который Guid.TryParse понимает,
        // но regex не вытащил как отдельный фрагмент.
        if (result.Count == 0 && Guid.TryParse(value, out var parsed))
        {
            result.Add(parsed);
        }

        return result.ToArray();
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
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
        IReadOnlyList<SearchHitRow> hits,
        CancellationToken ct)
    {
        var resultNodeIds = hits
            .Where(x => x.Kind == HitKindNode)
            .Select(x => x.Id)
            .ToHashSet();

        var fileParentNodeIds = hits
            .Where(x => x.Kind == HitKindFile)
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
            ct);

        var nodePaths = new Dictionary<Guid, string>(resultNodeIds.Count);
        foreach (var nodeId in resultNodeIds)
        {
            nodePaths[nodeId] = allNodePaths.TryGetValue(nodeId, out var path)
                ? path
                : Constants.DefaultPathSeparator.ToString();
        }

        var filePaths = new Dictionary<Guid, string>();
        foreach (var hit in hits.Where(x => x.Kind == HitKindFile))
        {
            var parentPath = allNodePaths.TryGetValue(hit.NodeIdForPath, out var path)
                ? path
                : Constants.DefaultPathSeparator.ToString();

            filePaths[hit.Id] = CombinePath(parentPath, hit.Name);
        }

        return (nodePaths, filePaths);
    }

    private static string CombinePath(string parentPath, string name)
    {
        var separator = Constants.DefaultPathSeparator;

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

    private async Task<Dictionary<Guid, (Guid? ParentId, string Name, int Type)>> LoadNodeLineageAsync(
        Guid userId,
        Guid layoutId,
        HashSet<Guid> startNodeIds,
        CancellationToken ct)
    {
        Dictionary<Guid, (Guid? ParentId, string Name, int Type)> nodeInfo = [];

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
                .Select(x => new { x.Id, x.ParentId, x.Name, x.Type })
                .ToListAsync(ct);

            foreach (var n in chunk)
            {
                if (nodeInfo.ContainsKey(n.Id))
                {
                    continue;
                }

                nodeInfo[n.Id] = (n.ParentId, n.Name, (int)n.Type);

                if (n.ParentId.HasValue && !nodeInfo.ContainsKey(n.ParentId.Value))
                {
                    frontier.Add(n.ParentId.Value);
                }
            }
        }

        // Защита от случайной связи между разными деревьями/root-type.
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

        var currentId = id;
        var depth = 0;

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

    private sealed record SearchCriteria(
        string NameKey,
        string ContainsPattern,
        string PrefixPattern,
        Guid?[] IdQueries)
    {
        public bool HasText => NameKey.Length > 0;
        public bool HasIds => IdQueries.Length > 0;
    }

    private sealed class SearchHitRow
    {
        public int Kind { get; set; }
        public Guid Id { get; set; }
        public Guid NodeIdForPath { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameKey { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}