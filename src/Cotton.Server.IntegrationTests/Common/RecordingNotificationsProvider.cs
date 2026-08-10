// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;

namespace Cotton.Server.IntegrationTests.Common
{
    internal class RecordingNotificationsProvider : INotificationsProvider
    {
        public bool ThrowOnEmail { get; set; }

        public List<(
            Guid UserId,
            EmailTemplate Template,
            Dictionary<string, string> Parameters,
            string ServerBaseUrl,
            string? RecipientEmail)> Emails { get; } = [];

        public Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            string? recipientEmail = null)
        {
            if (ThrowOnEmail)
            {
                throw new InvalidOperationException("Email delivery failed.");
            }

            Emails.Add((
                userId,
                template,
                new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase),
                serverBaseUrl,
                recipientEmail));
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
