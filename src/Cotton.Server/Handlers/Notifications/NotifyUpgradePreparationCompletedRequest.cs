// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Notifications
{
    public record NotifyUpgradePreparationCompletedRequest(string JobName, string TargetVersion) : IRequest;

    public class NotifyUpgradePreparationCompletedRequestHandler(
        CottonDbContext dbContext,
        INotificationsProvider notifications,
        ILogger<NotifyUpgradePreparationCompletedRequestHandler> logger)
        : IRequestHandler<NotifyUpgradePreparationCompletedRequest>
    {
        public async Task Handle(NotifyUpgradePreparationCompletedRequest request, CancellationToken cancellationToken)
        {
            string title = NotificationTemplates.UpgradePreparationCompletedTitle(request.TargetVersion);
            string content = NotificationTemplates.UpgradePreparationCompletedContent(request.JobName, request.TargetVersion);
            List<Guid> adminIds = await dbContext.Users
                .Where(user => user.Role == UserRole.Admin)
                .Where(user => !dbContext.Notifications.Any(notification => notification.UserId == user.Id
                    && notification.Title == title && notification.Content == content))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);

            Dictionary<string, string> metadata = NotificationTemplateMetadata.Create(
                NotificationTemplateKeys.UpgradePreparationCompletedTitle,
                NotificationTemplateKeys.UpgradePreparationCompletedContent,
                new Dictionary<string, string>
                {
                    ["jobName"] = request.JobName,
                    ["targetVersion"] = request.TargetVersion,
                });
            foreach (Guid adminId in adminIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await notifications.SendNotificationAsync(
                    adminId,
                    title,
                    content,
                    NotificationPriority.Medium,
                    metadata);
            }

            logger.LogInformation("Upgrade preparation job {JobName} for Cotton {TargetVersion} completed successfully.",
                request.JobName, request.TargetVersion);
        }
    }
}
