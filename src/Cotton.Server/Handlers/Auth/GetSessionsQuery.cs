// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Auth
{
    /// <summary>
    /// Represents a get sessions query sent through the mediator pipeline.
    /// </summary>
    public record GetSessionsQuery(Guid UserId, string SessionId) : IRequest<IEnumerable<SessionDto>> { }

    /// <summary>
    /// Handles get sessions queries in the mediator pipeline.
    /// </summary>
    public class GetSessionsQueryHandler(
        CottonDbContext _dbContext,
        ITokenProvider _tokens) : IRequestHandler<GetSessionsQuery, IEnumerable<SessionDto>>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<IEnumerable<SessionDto>> Handle(GetSessionsQuery request, CancellationToken cancellationToken)
        {
            List<ExtendedRefreshToken> tokens = await LoadTokensAsync(request.UserId, cancellationToken);
            TimeSpan tokenLifetime = _tokens.TokenLifetime;
            DateTime now = DateTime.UtcNow;

            return tokens
                .GroupBy(x => x.SessionId!)
                .Where(HasAnyNonRevokedRefreshToken)
                .Select(g => CreateSessionDto(g, request.SessionId, tokenLifetime))
                .OrderByDescending(x => x.TotalSessionDuration);
        }

        private Task<List<ExtendedRefreshToken>> LoadTokensAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return _dbContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.SessionId != null)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken: cancellationToken);
        }

        private static bool HasAnyNonRevokedRefreshToken(
            IGrouping<string, ExtendedRefreshToken> tokens)
            => tokens.Any(t => t.RevokedAt == null);

        private static SessionDto CreateSessionDto(
            IGrouping<string, ExtendedRefreshToken> tokens,
            string currentSessionId,
            TimeSpan tokenLifetime)
        {
            ExtendedRefreshToken? latestActive = tokens
                .Where(x => x.RevokedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            ExtendedRefreshToken latestAny = tokens
                .OrderByDescending(x => x.CreatedAt)
                .First();

            ExtendedRefreshToken source = latestActive ?? latestAny;
            TimeSpan totalSessionDuration = CalculateTotalSessionDuration(tokens, tokenLifetime);

            return new SessionDto
            {
                LastSeenAt = latestAny.CreatedAt,
                IsCurrentSession = currentSessionId == tokens.Key,
                SessionId = tokens.Key,
                IpAddress = source.IpAddress.ToString(),
                UserAgent = source.UserAgent,
                AuthType = source.AuthType,
                Country = source.Country ?? "Unknown",
                Region = source.Region ?? "Unknown",
                City = source.City ?? "Unknown",
                Device = source.Device ?? "Unknown",
                RefreshTokenCount = tokens.Count(),
                TotalSessionDuration = totalSessionDuration
            };
        }

        private static TimeSpan CalculateTotalSessionDuration(
            IEnumerable<ExtendedRefreshToken> tokens,
            TimeSpan tokenLifetime)
        {
            var intervals = tokens
                .Select(t =>
                {
                    DateTime start = t.CreatedAt;
                    DateTime endByTtl = t.CreatedAt + tokenLifetime;
                    DateTime endByRevoke = t.RevokedAt ?? endByTtl;
                    DateTime end = endByRevoke < endByTtl ? endByRevoke : endByTtl;
                    return (start, end);
                })
                .Where(x => x.end > x.start)
                .OrderBy(x => x.start)
                .ToList();

            return SumMergedIntervals(intervals);
        }

        private static TimeSpan SumMergedIntervals(List<(DateTime start, DateTime end)> intervals)
        {
            if (intervals.Count == 0)
            {
                return TimeSpan.Zero;
            }

            DateTime currentStart = intervals[0].start;
            DateTime currentEnd = intervals[0].end;
            TimeSpan total = TimeSpan.Zero;

            for (int i = 1; i < intervals.Count; i++)
            {
                var (s, e) = intervals[i];
                if (s <= currentEnd)
                {
                    if (e > currentEnd)
                    {
                        currentEnd = e;
                    }
                }
                else
                {
                    total += currentEnd - currentStart;
                    currentStart = s;
                    currentEnd = e;
                }
            }

            total += currentEnd - currentStart;
            return total;
        }
    }
}
