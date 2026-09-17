// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using EasyExtensions.Models.Enums;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class NoopNotificationsProvider : INotificationsProvider
    {
        public Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            string? recipientEmail = null)
        {
            return Task.FromResult(true);
        }

        public Task SendSmtpTestEmailAsync(Guid userId, string serverBaseUrl)
        {
            return Task.CompletedTask;
        }

        public Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null)
        {
            return Task.CompletedTask;
        }
    }
}
