// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the notifications provider contract used by the server runtime.
    /// </summary>
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

        /// <summary>
        /// Sends smtp test email.
        /// </summary>
        Task SendSmtpTestEmailAsync(
            Guid userId,
            string serverBaseUrl);

        /// <summary>
        /// Sends notification.
        /// </summary>
        Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null);
    }
}
