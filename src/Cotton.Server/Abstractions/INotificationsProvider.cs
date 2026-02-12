using Cotton.Database.Models.Enums;

namespace Cotton.Server.Abstractions
{
    public interface INotificationsProvider
    {
        Task SendEmailAsync(Guid userId, string subject, string body);
        Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null);
    }
}