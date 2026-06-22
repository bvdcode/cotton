// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using System.Net;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Contains extension methods for configuring notifications provider.
    /// </summary>
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
        /// Sends failed login attempt async.
        /// </summary>
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
                        username,
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.FailedLoginAttemptContentNoDevice(
                        username,
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.FailedLoginAttemptTitle, contentKey));
        }

        /// <summary>
        /// Sends otp disabled async.
        /// </summary>
        public static async Task SendOtpDisabledAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.OtpDisabledTitle,
                content: context.HasDevice
                    ? NotificationTemplates.OtpDisabledContent(
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.OtpDisabledContentNoDevice(
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.OtpDisabledTitle, contentKey));
        }

        /// <summary>
        /// Sends otp enabled async.
        /// </summary>
        public static async Task SendOtpEnabledAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.OtpEnabledTitle,
                content: context.HasDevice
                    ? NotificationTemplates.OtpEnabledContent(
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.OtpEnabledContentNoDevice(
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.Medium,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.OtpEnabledTitle, contentKey));
        }

        /// <summary>
        /// Sends successful login async.
        /// </summary>
        public static async Task SendSuccessfulLoginAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SuccessfulLoginTitle,
                content: context.HasDevice
                    ? NotificationTemplates.SuccessfulLoginContent(
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.SuccessfulLoginContentNoDevice(
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.None,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.SuccessfulLoginTitle, contentKey));
        }

        /// <summary>
        /// Sends totp failed attempt async.
        /// </summary>
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
                        totpFailedAttempts,
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.TotpFailedAttemptContentNoDevice(
                        totpFailedAttempts,
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.Medium,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.TotpFailedAttemptTitle, contentKey));
        }

        /// <summary>
        /// Sends totp lockout async.
        /// </summary>
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
                        maxFailedAttempts,
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.TotpLockoutContentNoDevice(
                        maxFailedAttempts,
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.High,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.TotpLockoutTitle, contentKey));
        }

        /// <summary>
        /// Sends web dav token reset async.
        /// </summary>
        public static async Task SendWebDavTokenResetAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.WebDavTokenResetTitle,
                content: context.HasDevice
                    ? NotificationTemplates.WebDavTokenResetContent(
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.WebDavTokenResetContentNoDevice(
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.Medium,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.WebDavTokenResetTitle, contentKey));
        }

        /// <summary>
        /// Sends shared file downloaded notification async.
        /// </summary>
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
                        fileName,
                        context.Ip,
                        context.DeviceName,
                        context.Location)
                    : NotificationTemplates.SharedFileDownloadedContentNoDevice(
                        fileName,
                        context.Ip,
                        context.Location),
                priority: NotificationPriority.None,
                metadata: CreateTemplateMetadata(metadata, NotificationTemplateKeys.SharedFileDownloadedTitle, contentKey));
        }

        /// <summary>
        /// Sends upload hash mismatch notification async.
        /// </summary>
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

        /// <summary>
        /// Sends storage chunk missing notification async.
        /// </summary>
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
