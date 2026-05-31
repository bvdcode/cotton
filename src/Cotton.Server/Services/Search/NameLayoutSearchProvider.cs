// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.Search;

/// <summary>
/// Searches layout items by their normalized display names and identifiers.
/// </summary>
public sealed class NameLayoutSearchProvider(CottonDbContext _dbContext) : ILayoutSearchProvider
{
    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public bool CanSearch(LayoutSearchCriteria criteria)
    {
        return criteria.HasText || criteria.HasIds;
    }

    /// <inheritdoc />
    public IQueryable<LayoutSearchHit> BuildHitsQuery(LayoutSearchProviderContext context)
    {
        return BuildNodeHitsQuery(context)
            .Concat(BuildFileHitsQuery(context));
    }

    private IQueryable<LayoutSearchHit> BuildNodeHitsQuery(LayoutSearchProviderContext context)
    {
        var request = context.Request;
        var criteria = context.Criteria;

        var query = _dbContext.Nodes
            .AsNoTracking()
            .Where(x => x.OwnerId == request.UserId
                && x.LayoutId == request.LayoutId
                && x.Type == NodeType.Default);

        query = criteria.HasIds
            ? query.Where(x => criteria.IdQueries.Contains(x.Id))
            : ApplyTextCriteria(query, criteria);

        return query.Select(x => new LayoutSearchHit
        {
            Kind = LayoutSearchHitKind.Node,
            Id = x.Id,
            NodeIdForPath = x.Id,
            Name = x.Name,
            NameKey = x.NameKey,
            Score =
                criteria.HasIds && criteria.IdQueries.Contains(x.Id) ? 100 :
                criteria.HasText && x.NameKey == criteria.NameKey ? 80 :
                criteria.HasText && EF.Functions.Like(x.NameKey, criteria.PrefixPattern, criteria.LikeEscape) ? 60 :
                criteria.HasMultipleTextTokens ? 40 :
                20,
        });
    }

    private IQueryable<LayoutSearchHit> BuildFileHitsQuery(LayoutSearchProviderContext context)
    {
        var request = context.Request;
        var criteria = context.Criteria;

        var query = _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == request.UserId
                && x.Node.LayoutId == request.LayoutId
                && x.Node.Type == NodeType.Default);

        query = criteria.HasIds
            ? query.Where(x => criteria.IdQueries.Contains(x.Id) || criteria.IdQueries.Contains(x.FileManifestId))
            : ApplyTextCriteria(query, criteria);

        return query.Select(x => new LayoutSearchHit
        {
            Kind = LayoutSearchHitKind.File,
            Id = x.Id,
            NodeIdForPath = x.NodeId,
            Name = x.Name,
            NameKey = x.NameKey,
            Score =
                criteria.HasIds && criteria.IdQueries.Contains(x.Id) ? 100 :
                criteria.HasIds && criteria.IdQueries.Contains(x.FileManifestId) ? 95 :
                criteria.HasText && x.NameKey == criteria.NameKey ? 80 :
                criteria.HasText && EF.Functions.Like(x.NameKey, criteria.PrefixPattern, criteria.LikeEscape) ? 60 :
                criteria.HasMultipleTextTokens ? 40 :
                20,
        });
    }

    private static IQueryable<Node> ApplyTextCriteria(
        IQueryable<Node> query,
        LayoutSearchCriteria criteria)
    {
        if (criteria.HasMultipleTextTokens)
        {
            foreach (LayoutSearchToken token in criteria.TextTokens)
            {
                query = query.Where(x => EF.Functions.Like(x.NameKey, token.ContainsPattern, criteria.LikeEscape));
            }

            return query;
        }

        return query.Where(x => EF.Functions.Like(x.NameKey, criteria.ContainsPattern, criteria.LikeEscape));
    }

    private static IQueryable<NodeFile> ApplyTextCriteria(
        IQueryable<NodeFile> query,
        LayoutSearchCriteria criteria)
    {
        if (criteria.HasMultipleTextTokens)
        {
            foreach (LayoutSearchToken token in criteria.TextTokens)
            {
                query = query.Where(x => EF.Functions.Like(x.NameKey, token.ContainsPattern, criteria.LikeEscape));
            }

            return query;
        }

        return query.Where(x => EF.Functions.Like(x.NameKey, criteria.ContainsPattern, criteria.LikeEscape));
    }
}
