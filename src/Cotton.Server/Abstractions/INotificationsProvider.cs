using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;

namespace Cotton.Server.Abstractions
{
    public interface INotificationsProvider
    {
        /// <summary>
        /// Sends an email to the specified user. Returns true if the email was
        /// actually dispatched, false if it was skipped or delivery failed.
        /// </summary>
        Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl);

        Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null);
    }
}