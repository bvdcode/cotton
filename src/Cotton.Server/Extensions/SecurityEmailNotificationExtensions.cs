// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Email;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging;
using System.Net;

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
                    [EmailTemplateParameterNames.SecurityTitle] = EncodeText(title),
                    [EmailTemplateParameterNames.SecurityContent] = EncodeMultilineText(content),
                    [EmailTemplateParameterNames.OccurredAt] = EncodeText(
                        SecurityEmailTimestampFormatter.Format(occurredAt, timeZone)),
                    [EmailTemplateParameterNames.ServerUrl] = WebUtility.HtmlEncode(serverBaseUrl),
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

        private static string EncodeText(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string EncodeMultilineText(string? value)
        {
            return EncodeText(value)
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal);
        }
    }
}
