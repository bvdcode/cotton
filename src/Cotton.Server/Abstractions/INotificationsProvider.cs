using Cotton.Database.Models.Enums;
using Microsoft.Extensions.Primitives;
using System.Net;

namespace Cotton.Server.Abstractions
{
    public interface INotificationsProvider
    {
        Task SendEmailAsync(Guid userId);
        Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null);
    }
}