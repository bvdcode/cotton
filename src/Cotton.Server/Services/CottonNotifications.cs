using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Hubs;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.Quartz.Extensions;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Quartz;

namespace Cotton.Server.Services
{
    public class CottonNotifications(
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        IHubContext<EventHub> _hubContext) : INotificationsProvider
    {
        public Task SendEmailAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public async Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null)
        {
            Notification notification = new()
            {
                Title = title,
                UserId = userId,
                Content = content,
                Priority = priority,
                Metadata = metadata ?? []
            };
            await _dbContext.Notifications.AddAsync(notification);
            await _dbContext.SaveChangesAsync();
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                EventHub.NotificationMethod,
                notification.Adapt<NotificationDto>());
        }
    }
}
