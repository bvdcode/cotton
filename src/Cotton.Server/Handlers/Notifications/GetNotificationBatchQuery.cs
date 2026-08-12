// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Data;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.Notifications
{
    public class GetNotificationBatchQuery(
        Guid userId,
        NotificationCursorDto? cursor,
        int detailLimit) : IRequest<NotificationBatchDto>
    {
        public Guid UserId { get; } = userId;

        public NotificationCursorDto? Cursor { get; } = cursor;

        public int DetailLimit { get; } = detailLimit;
    }

    public class GetNotificationBatchQueryHandler(CottonDbContext _dbContext)
        : IRequestHandler<GetNotificationBatchQuery, NotificationBatchDto>
    {
        private const int MaximumDetailLimit = 100;

        public async Task<NotificationBatchDto> Handle(
            GetNotificationBatchQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DetailLimit);
            int detailLimit = Math.Min(request.DetailLimit, MaximumDetailLimit);
            IExecutionStrategy executionStrategy = _dbContext.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.RepeatableRead,
                    cancellationToken);

                IQueryable<Notification> newNotifications = ApplyCursor(
                    _dbContext.Notifications
                        .AsNoTracking()
                        .Where(notification => notification.UserId == request.UserId),
                    request.Cursor);

                NotificationCursorDto? nextCursor = await newNotifications
                    .OrderByDescending(notification => notification.CreatedAt)
                    .ThenByDescending(notification => notification.Id)
                    .Select(notification => new NotificationCursorDto
                    {
                        CreatedAt = notification.CreatedAt,
                        NotificationId = notification.Id,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                IQueryable<Notification> unreadNotifications = newNotifications
                    .Where(notification => !notification.ReadAt.HasValue);
                int unreadCount = await unreadNotifications.CountAsync(cancellationToken);
                List<NotificationDto> details = await unreadNotifications
                    .OrderByDescending(notification => notification.CreatedAt)
                    .ThenByDescending(notification => notification.Id)
                    .Take(detailLimit)
                    .Select(notification => new NotificationDto
                    {
                        Id = notification.Id,
                        CreatedAt = notification.CreatedAt,
                        UpdatedAt = notification.UpdatedAt,
                        Title = notification.Title,
                        Content = notification.Content,
                        ReadAt = notification.ReadAt,
                        Metadata = notification.Metadata,
                        UserId = notification.UserId,
                        Priority = notification.Priority,
                    })
                    .ToListAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return new NotificationBatchDto
                {
                    UnreadNotifications = details,
                    UnreadCount = unreadCount,
                    NextCursor = nextCursor ?? request.Cursor,
                };
            });
        }

        private static IQueryable<Notification> ApplyCursor(
            IQueryable<Notification> notifications,
            NotificationCursorDto? cursor)
        {
            if (cursor is null)
            {
                return notifications;
            }

            return notifications.Where(notification =>
                notification.CreatedAt > cursor.CreatedAt
                || (notification.CreatedAt == cursor.CreatedAt
                    && notification.Id.CompareTo(cursor.NotificationId) > 0));
        }
    }
}
