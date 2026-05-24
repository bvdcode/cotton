// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents a admin get users query sent through the mediator pipeline.
    /// </summary>
    public class AdminGetUsersQuery : IRequest<IEnumerable<AdminUserDto>>
    { 
        /// <summary>
        /// Gets or sets the calculate storage usage.
        /// </summary>
        public bool CalculateStorageUsage { get; set; }
    }

    /// <summary>
    /// Handles admin get users queries in the mediator pipeline.
    /// </summary>
    public class AdminGetUsersQueryHandler(CottonDbContext _dbContext) : IRequestHandler<AdminGetUsersQuery, IEnumerable<AdminUserDto>>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<IEnumerable<AdminUserDto>> Handle(AdminGetUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _dbContext.Users
                .AsNoTracking()
                .OrderBy(x => x.Username)
                .ToListAsync(cancellationToken);

            var userIds = users.Select(x => x.Id).ToList();

            var activity = await _dbContext.RefreshTokens
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId) && x.RevokedAt == null && x.SessionId != null)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastActivityAt = g.Max(x => x.CreatedAt),
                    ActiveSessionCount = g.Select(x => x.SessionId!).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.UserId, x => x, cancellationToken);

            Dictionary<Guid, long> storageUsage = request.CalculateStorageUsage ? await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => userIds.Contains(x.OwnerId))
                .GroupBy(x => x.OwnerId)
                .Select(g => new
                {
                    UserId = g.Key,
                    StorageUsedBytes = g.Sum(x => x.FileManifest.SizeBytes)
                })
                .ToDictionaryAsync(x => x.UserId, x => x.StorageUsedBytes, cancellationToken) : [];

            return users.Select(u =>
            {
                activity.TryGetValue(u.Id, out var a);
                storageUsage.TryGetValue(u.Id, out long storageUsedBytes);
                return new AdminUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    IsEmailVerified = u.IsEmailVerified,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    BirthDate = u.BirthDate,
                    Role = u.Role,
                    IsTotpEnabled = u.IsTotpEnabled,
                    TotpEnabledAt = u.TotpEnabledAt,
                    TotpFailedAttempts = u.TotpFailedAttempts,
                    LastActivityAt = a?.LastActivityAt,
                    ActiveSessionCount = a?.ActiveSessionCount ?? 0,
                    StorageUsedBytes = storageUsedBytes,
                };
            });
        }
    }
}
