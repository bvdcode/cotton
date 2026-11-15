// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Notifications;
using EasyExtensions.Mediator;
using System.Collections.Concurrent;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class SharedUploadNotificationRecorder(IServiceScopeFactory scopes) : INotificationHandler<FileUploadedNotification>
    {
        public ConcurrentQueue<FileUploadedNotification> Uploads { get; } = new();

        public async Task Handle(FileUploadedNotification notification, CancellationToken ct)
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            bool committed = await db.NodeFiles.AnyAsync(file => file.Id == notification.NodeFileId
                && file.NodeId == notification.NodeId && file.OwnerId == notification.OwnerId, ct);
            if (!committed)
            {
                throw new InvalidOperationException("Upload event arrived before the file was committed.");
            }
            Uploads.Enqueue(notification);
        }
    }
}
