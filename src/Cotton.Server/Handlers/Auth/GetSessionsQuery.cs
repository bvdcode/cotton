using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Auth
{
    public record GetSessionsQuery(Guid UserId, string SessionId) : IRequest<IEnumerable<SessionDto>> { }

    public class GetSessionsQueryHandler(
        CottonDbContext _dbContext,
        ITokenProvider _tokens) : IRequestHandler<GetSessionsQuery, IEnumerable<SessionDto>>
    {
        public async Task<IEnumerable<SessionDto>> Handle(GetSessionsQuery request, CancellationToken cancellationToken)
        {
            var tokens = await _dbContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == request.UserId && x.SessionId != null)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken: cancellationToken);

            TimeSpan tokenLifetime = _tokens.TokenLifetime;
            return tokens.GroupBy(x => x.SessionId!).Select(g =>
            {
                var latestActive = g
                    .Where(x => x.RevokedAt == null)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                var latestAny = g
                    .OrderByDescending(x => x.CreatedAt)
                    .First();

                var source = latestActive ?? latestAny;
                var earliestCreatedAt = g.Min(x => x.CreatedAt);
                var latestCreatedAt = g.Max(x => x.CreatedAt);

                var intervals = g
                    .Select(t =>
                    {
                        var start = t.CreatedAt;
                        var endByTtl = t.CreatedAt + tokenLifetime;
                        var endByRevoke = t.RevokedAt ?? endByTtl;
                        var end = endByRevoke < endByTtl ? endByRevoke : endByTtl;
                        return (start, end);
                    })
                    .Where(x => x.end > x.start)
                    .OrderBy(x => x.start)
                    .ToList();

                TimeSpan totalSessionDuration = TimeSpan.Zero;
                if (intervals.Count > 0)
                {
                    var currentStart = intervals[0].start;
                    var currentEnd = intervals[0].end;

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
                            totalSessionDuration += currentEnd - currentStart;
                            currentStart = s;
                            currentEnd = e;
                        }
                    }

                    totalSessionDuration += currentEnd - currentStart;
                }

                return new SessionDto
                {
                    LastSeenAt = latestAny.CreatedAt,
                    IsCurrentSession = request.SessionId == g.Key,
                    SessionId = g.Key,
                    IpAddress = source.IpAddress.ToString(),
                    UserAgent = source.UserAgent,
                    AuthType = source.AuthType,
                    Country = source.Country ?? "Unknown",
                    Region = source.Region ?? "Unknown",
                    City = source.City ?? "Unknown",
                    Device = source.Device ?? "Unknown",
                    RefreshTokenCount = g.Count(),
                    TotalSessionDuration = totalSessionDuration
                };
            }).OrderByDescending(x => x.TotalSessionDuration);
        }
    }
}
