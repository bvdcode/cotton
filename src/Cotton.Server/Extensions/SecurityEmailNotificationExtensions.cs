// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Email;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging;

namespace Cotton.Server.Extensions
{
    public static class SecurityEmailNotificationExtensions
    {
        public static async Task SendSecurityEmailAsync(
            this INotificationsProvider notifications,
            SettingsProvider settings,
            ILogger logger,
            Guid userId,
            string title,
            string? content,
            DateTime occurredAt,
            string? recipientEmail = null)
        {
            ArgumentNullException.ThrowIfNull(notifications);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logger);

            try
            {
                string serverBaseUrl = (await settings.GetPublicBaseUrlAsync(CancellationToken.None)).TrimEnd('/');
                TimeZoneInfo timeZone = settings.GetServerSettings().GetTimezoneInfo();
                Dictionary<string, string> parameters = new()
                {
                    [EmailTemplateParameterNames.SecurityTitle] = title,
                    [EmailTemplateParameterNames.SecurityContent] = content ?? string.Empty,
                    [EmailTemplateParameterNames.OccurredAt] = SecurityEmailTimestampFormatter.Format(occurredAt, timeZone),
                };

                bool sent = await notifications.SendEmailAsync(
                    userId,
                    EmailTemplate.SecurityAlert,
                    parameters,
                    serverBaseUrl,
                    recipientEmail);
                if (!sent)
                {
                    logger.LogDebug("Security email was not sent for user {UserId}.", userId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Security email delivery failed for user {UserId}.", userId);
            }
        }
    }
}
