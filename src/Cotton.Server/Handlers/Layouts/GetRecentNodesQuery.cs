// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.Handlers.Layouts
{
    public class GetRecentNodesQuery(
        Guid userId,
        Guid layoutId,
        int count,
        IReadOnlyCollection<string>? contentTypes = null,
        IReadOnlyCollection<string>? excludedContentTypes = null)
        : IRequest<IEnumerable<NodeFileManifestDto>>
    {
        public int Count { get; } = count;

        public Guid UserId { get; } = userId;

        public Guid LayoutId { get; } = layoutId;

        public IReadOnlyCollection<string> ContentTypes { get; } = contentTypes ?? [];

        public IReadOnlyCollection<string> ExcludedContentTypes { get; } = excludedContentTypes ?? [];
    }

    public class GetRecentNodesQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetRecentNodesQuery, IEnumerable<NodeFileManifestDto>>
    {
        private const int MaximumContentTypePatterns = 16;
        private const int MaximumContentTypePatternLength = 127;
        private const string MediaTypePattern = "^[a-z0-9][a-z0-9!#$&^_.+-]*/(?:\\*|[a-z0-9][a-z0-9!#$&^_.+-]*)$";

        public async Task<IEnumerable<NodeFileManifestDto>> Handle(GetRecentNodesQuery request, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Count);
            string? includedPattern = BuildRegexPattern(request.ContentTypes);
            string? excludedPattern = BuildRegexPattern(request.ExcludedContentTypes);

            IQueryable<NodeFile> query = _dbContext.NodeFiles
                .AsNoTracking()
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Node.Type == NodeType.Default)
                .Where(x => x.OwnerId == request.UserId && x.Node.LayoutId == request.LayoutId)
                .OrderByDescending(x => x.CreatedAt);

            if (includedPattern is not null)
            {
                query = query.Where(x => Regex.IsMatch(
                    x.FileManifest.ContentType,
                    includedPattern,
                    RegexOptions.IgnoreCase));
            }

            if (excludedPattern is not null)
            {
                query = query.Where(x => !Regex.IsMatch(
                    x.FileManifest.ContentType,
                    excludedPattern,
                    RegexOptions.IgnoreCase));
            }

            return await query
                .Take(request.Count)
                .ProjectToType<NodeFileManifestDto>()
                .ToListAsync(ct);
        }

        private static string? BuildRegexPattern(IReadOnlyCollection<string> values)
        {
            string[] normalized = values
                .Select(value => value.Trim().ToLowerInvariant())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length == 0)
            {
                return null;
            }

            if (normalized.Length > MaximumContentTypePatterns)
            {
                throw new BadRequestException(
                    $"A maximum of {MaximumContentTypePatterns} content type patterns is allowed.");
            }

            List<string> alternatives = new(normalized.Length);
            foreach (string value in normalized)
            {
                if (value.Length > MaximumContentTypePatternLength
                    || !Regex.IsMatch(value, MediaTypePattern, RegexOptions.CultureInvariant))
                {
                    throw new BadRequestException($"Invalid content type pattern: {value}");
                }

                alternatives.Add(value.EndsWith("/*", StringComparison.Ordinal)
                    ? Regex.Escape(value[..^1]) + "[^;]+"
                    : Regex.Escape(value));
            }

            return $"^(?:{string.Join('|', alternatives)})(?:[[:space:]]*;.*)?$";
        }

    }
}
