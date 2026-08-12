// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;

namespace Cotton.Server.Abstractions
{
    public interface INotificationsProvider
    {
        Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            string? recipientEmail = null);

        Task SendSmtpTestEmailAsync(
            Guid userId,
            string serverBaseUrl);

        Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null);
    }
}
