// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Email;
using Cotton.Localization;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using System.Net;

namespace Cotton.Server.Extensions
{
    public static class NotificationsProviderExtensions
    {
        private const string UnknownGeoLabel = "Unknown";
        private const string UnknownLocationLabel = "unknown location";
        private const string LocalNetworkLocationLabel = "local network";

        private record ClientNotificationContext(
            string Ip,
            string UserAgent,
            string DeviceName,
            bool HasDevice,
            string Location,
            string Country,
            string Region,
            string City);

        private static async Task<ClientNotificationContext> CreateClientContextAsync(
            IGeoLookupService geoLookup,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            string ip = ipAddress.ToString();
            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            string deviceName = device.FriendlyName ?? device.Type.ToString();
            bool isLocalNetwork = NetworkAddressClassifier.IsLocalNetworkAddress(ipAddress);
            GeoLookupResult? ipInfo = isLocalNetwork
                ? null
                : await geoLookup.TryLookupAsync(ipAddress);

            return new ClientNotificationContext(
                Ip: ip,
                UserAgent: userAgent.ToString(),
                DeviceName: deviceName,
                HasDevice: HasKnownDevice(deviceName),
                Location: isLocalNetwork
                    ? LocalNetworkLocationLabel
                    : FormatGeoLocation(ipInfo),
                Country: NormalizeGeoField(ipInfo?.Country),
                Region: NormalizeGeoField(ipInfo?.Region),
                City: NormalizeGeoField(ipInfo?.City));
        }

        private static bool HasKnownDevice(string deviceName)
        {
            return !string.IsNullOrWhiteSpace(deviceName)
                && !string.Equals(deviceName, UnknownGeoLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeGeoField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? UnknownGeoLabel : value;
        }

        private static bool IsKnownGeoField(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value.Trim(), UnknownGeoLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatGeoLocation(GeoLookupResult? ipInfo)
        {
            if (ipInfo is null)
            {
                return UnknownLocationLabel;
            }

            string[] parts = new[] { ipInfo.City, ipInfo.Region, ipInfo.Country }
                .Where(IsKnownGeoField)
                .Select(value => value!.Trim())
                .ToArray();

            return parts.Length == 0
                ? UnknownLocationLabel
                : string.Join(", ", parts);
        }

        private static Dictionary<string, string> CreateBaseMetadata(ClientNotificationContext context)
        {
            return new Dictionary<string, string>
            {
                ["ip"] = context.Ip,
                ["userAgent"] = context.UserAgent,
                ["device"] = context.DeviceName,
                ["location"] = context.Location,
                ["country"] = context.Country,
                ["region"] = context.Region,
                ["city"] = context.City
            };
        }

        private static Dictionary<string, string> CreateTemplateMetadata(
            Dictionary<string, string> metadata,
            string titleKey,
            string contentKey)
        {
            return NotificationTemplateMetadata.Create(titleKey, contentKey, metadata);
        }

        private static string FormatInvariant(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sends an account security email without affecting the completed account action.
        /// </summary>
        /// <remarks>
        /// Delivery is intentionally best-effort and request-scoped: it is not persisted or retried. Transport
        /// failures are logged and do not change the result of the originating account operation.
        /// </remarks>
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

        private static async Task SendSecurityEventAsync(
            INotificationsProvider notifications,
            SettingsProvider settings,
            ILogger logger,
            Guid userId,
            string title,
            string notificationContent,
            string emailContent,
            NotificationPriority priority,
            Dictionary<string, string> metadata)
        {
            DateTime occurredAt = DateTime.UtcNow;
            await notifications.SendNotificationAsync(
                userId,
                title,
                notificationContent,
                priority,
                metadata);
            await notifications.SendSecurityEmailAsync(
                settings,
                logger,
                userId,
                title,
                emailContent,
                occurredAt);
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

        public static async Task SendFailedLoginAttemptAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            Guid userId,
            string username,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            metadata["username"] = username;
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.FailedLoginAttemptWithDeviceContent
                : NotificationTemplateKeys.FailedLoginAttemptWithoutDeviceContent;

            await notifications.SendNotificationAsync(
                userId: userId,
                title: NotificationTemplates.FailedLoginAttemptTitle,
                content: context.HasDevice
                    ? NotificationTemplates.FailedLoginAttemptContent(
                        username: username,
                        ipAddress: null,
                        device: context.DeviceName,
                        location: context.Location)
                    : NotificationTemplates.FailedLoginAttemptContentNoDevice(
                        username: username,
                        ipAddress: null,
                        location: context.Location),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.FailedLoginAttemptTitle, contentKey));
        }

        public static async Task SendOtpDisabledAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            SettingsProvider settings,
            ILogger logger,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.OtpDisabledWithDeviceContent
                : NotificationTemplateKeys.OtpDisabledWithoutDeviceContent;
            string notificationContent = context.HasDevice
                ? NotificationTemplates.OtpDisabledContent(
                    ipAddress: null,
                    device: context.DeviceName,
                    location: context.Location)
                : NotificationTemplates.OtpDisabledContentNoDevice(
                    ipAddress: null,
                    location: context.Location);
            string emailContent = context.HasDevice
                ? NotificationTemplates.OtpDisabledContent(
                    context.Ip,
                    context.DeviceName,
                    context.Location)
                : NotificationTemplates.OtpDisabledContentNoDevice(
                    context.Ip,
                    context.Location);

            await SendSecurityEventAsync(
                notifications,
                settings,
                logger,
                userId,
                NotificationTemplates.OtpDisabledTitle,
                notificationContent,
                emailContent,
                NotificationPriority.High,
                CreateTemplateMetadata(metadata, NotificationTemplateKeys.OtpDisabledTitle, contentKey));
        }

        public static async Task SendOtpEnabledAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            SettingsProvider settings,
            ILogger logger,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.OtpEnabledWithDeviceContent
                : NotificationTemplateKeys.OtpEnabledWithoutDeviceContent;
            string notificationContent = context.HasDevice
                ? NotificationTemplates.OtpEnabledContent(
                    ipAddress: null,
                    device: context.DeviceName,
                    location: context.Location)
                : NotificationTemplates.OtpEnabledContentNoDevice(
                    ipAddress: null,
                    location: context.Location);
            string emailContent = context.HasDevice
                ? NotificationTemplates.OtpEnabledContent(
                    context.Ip,
                    context.DeviceName,
                    context.Location)
                : NotificationTemplates.OtpEnabledContentNoDevice(
                    context.Ip,
                    context.Location);

            await SendSecurityEventAsync(
                notifications,
                settings,
                logger,
                userId,
                NotificationTemplates.OtpEnabledTitle,
                notificationContent,
                emailContent,
                NotificationPriority.Medium,
                CreateTemplateMetadata(metadata, NotificationTemplateKeys.OtpEnabledTitle, contentKey));
        }

        public static async Task SendSuccessfulLoginAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            SettingsProvider settings,
            ILogger logger,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.SuccessfulLoginWithDeviceContent
                : NotificationTemplateKeys.SuccessfulLoginWithoutDeviceContent;
            string notificationContent = context.HasDevice
                ? NotificationTemplates.SuccessfulLoginContent(
                    ipAddress: null,
                    device: context.DeviceName,
                    location: context.Location)
                : NotificationTemplates.SuccessfulLoginContentNoDevice(
                    ipAddress: null,
                    location: context.Location);
            string emailContent = context.HasDevice
                ? NotificationTemplates.SuccessfulLoginContent(
                    context.Ip,
                    context.DeviceName,
                    context.Location)
                : NotificationTemplates.SuccessfulLoginContentNoDevice(
                    context.Ip,
                    context.Location);

            await SendSecurityEventAsync(
                notifications,
                settings,
                logger,
                userId,
                NotificationTemplates.SuccessfulLoginTitle,
                notificationContent,
                emailContent,
                NotificationPriority.None,
                CreateTemplateMetadata(metadata, NotificationTemplateKeys.SuccessfulLoginTitle, contentKey));
        }

        public static async Task SendTotpFailedAttemptAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            Guid userId,
            int totpFailedAttempts,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            metadata["totpFailedAttempts"] = FormatInvariant(totpFailedAttempts);
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.TotpFailedAttemptWithDeviceContent
                : NotificationTemplateKeys.TotpFailedAttemptWithoutDeviceContent;

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpFailedAttemptTitle,
                content: context.HasDevice
                    ? NotificationTemplates.TotpFailedAttemptContent(
                        failedAttempts: totpFailedAttempts,
                        ipAddress: null,
                        device: context.DeviceName,
                        location: context.Location)
                    : NotificationTemplates.TotpFailedAttemptContentNoDevice(
                        failedAttempts: totpFailedAttempts,
                        ipAddress: null,
                        location: context.Location),
                priority: NotificationPriority.Medium,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.TotpFailedAttemptTitle, contentKey));
        }

        public static async Task SendTotpLockoutAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            Guid userId,
            int maxFailedAttempts,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            metadata["maxFailedAttempts"] = FormatInvariant(maxFailedAttempts);
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.TotpLockoutWithDeviceContent
                : NotificationTemplateKeys.TotpLockoutWithoutDeviceContent;

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpLockoutTitle,
                content: context.HasDevice
                    ? NotificationTemplates.TotpLockoutContent(
                        maxFailedAttempts: maxFailedAttempts,
                        ipAddress: null,
                        device: context.DeviceName,
                        location: context.Location)
                    : NotificationTemplates.TotpLockoutContentNoDevice(
                        maxFailedAttempts: maxFailedAttempts,
                        ipAddress: null,
                        location: context.Location),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.TotpLockoutTitle, contentKey));
        }

        public static async Task SendWebDavTokenResetAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            SettingsProvider settings,
            ILogger logger,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.WebDavTokenResetWithDeviceContent
                : NotificationTemplateKeys.WebDavTokenResetWithoutDeviceContent;
            string notificationContent = context.HasDevice
                ? NotificationTemplates.WebDavTokenResetContent(
                    ipAddress: null,
                    device: context.DeviceName,
                    location: context.Location)
                : NotificationTemplates.WebDavTokenResetContentNoDevice(
                    ipAddress: null,
                    location: context.Location);
            string emailContent = context.HasDevice
                ? NotificationTemplates.WebDavTokenResetContent(
                    context.Ip,
                    context.DeviceName,
                    context.Location)
                : NotificationTemplates.WebDavTokenResetContentNoDevice(
                    context.Ip,
                    context.Location);

            await SendSecurityEventAsync(
                notifications,
                settings,
                logger,
                userId,
                NotificationTemplates.WebDavTokenResetTitle,
                notificationContent,
                emailContent,
                NotificationPriority.Medium,
                CreateTemplateMetadata(metadata, NotificationTemplateKeys.WebDavTokenResetTitle, contentKey));
        }

        public static async Task SendSharedFileDownloadedNotificationAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            Guid userId,
            string fileName,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            ClientNotificationContext context = await CreateClientContextAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = CreateBaseMetadata(context);
            metadata["fileName"] = fileName;
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.SharedFileDownloadedWithDeviceContent
                : NotificationTemplateKeys.SharedFileDownloadedWithoutDeviceContent;

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SharedFileDownloadedTitle,
                content: context.HasDevice
                    ? NotificationTemplates.SharedFileDownloadedContent(
                        fileName: fileName,
                        ipAddress: null,
                        device: context.DeviceName,
                        location: context.Location)
                    : NotificationTemplates.SharedFileDownloadedContentNoDevice(
                        fileName: fileName,
                        ipAddress: null,
                        location: context.Location),
                priority: NotificationPriority.None,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.SharedFileDownloadedTitle, contentKey));
        }

        public static async Task SendUploadHashMismatchNotificationAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string fileName,
            string proposedHash,
            string computedHash)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            Dictionary<string, string> metadata = new()
            {
                ["fileName"] = fileName,
                ["proposedHash"] = proposedHash,
                ["computedHash"] = computedHash,
                ["proposedTail"] = NotificationTemplates.FormatHashTail(proposedHash),
                ["computedTail"] = NotificationTemplates.FormatHashTail(computedHash)
            };

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.UploadHashMismatchTitle,
                content: NotificationTemplates.UploadHashMismatchContent(
                    fileName,
                    proposedHash,
                    computedHash),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.UploadHashMismatchTitle, NotificationTemplateKeys.UploadHashMismatchContent));
        }

        public static async Task SendStorageChunkMissingNotificationAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string fileName)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            Dictionary<string, string> metadata = new()
            {
                ["fileName"] = fileName
            };

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.StorageChunkMissingTitle,
                content: NotificationTemplates.StorageChunkMissingContent(fileName),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.StorageChunkMissingTitle, NotificationTemplateKeys.StorageChunkMissingContent));
        }
    }
}
