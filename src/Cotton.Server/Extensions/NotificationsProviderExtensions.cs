using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using EasyExtensions.Clients.Models;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Primitives;
using System.Net;

namespace Cotton.Server.Extensions
{
    public static class NotificationsProviderExtensions
    {
        private record ClientNotificationContext(
            string Ip,
            string UserAgent,
            string DeviceName,
            bool HasDevice,
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
            GeoIpInfo? ipInfo = await geoLookup.TryLookupAsync(ipAddress);

            return new ClientNotificationContext(
                Ip: ip,
                UserAgent: userAgent.ToString(),
                DeviceName: deviceName,
                HasDevice: HasKnownDevice(deviceName),
                Country: NormalizeGeoField(ipInfo?.Country),
                Region: NormalizeGeoField(ipInfo?.Region),
                City: NormalizeGeoField(ipInfo?.City));
        }

        private static bool HasKnownDevice(string deviceName)
        {
            return !string.IsNullOrWhiteSpace(deviceName)
                && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeGeoField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }

        private static Dictionary<string, string> CreateBaseMetadata(ClientNotificationContext context)
        {
            return new Dictionary<string, string>
            {
                ["ip"] = context.Ip,
                ["userAgent"] = context.UserAgent,
                ["device"] = context.DeviceName,
                ["country"] = context.Country,
                ["region"] = context.Region,
                ["city"] = context.City
            };
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

            await notifications.SendNotificationAsync(
                userId: userId,
                title: NotificationTemplates.FailedLoginAttemptTitle,
                content: context.HasDevice
                    ? NotificationTemplates.FailedLoginAttemptContent(
                        username,
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.FailedLoginAttemptContentNoDevice(
                        username,
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.High,
                metadata: metadata);
        }

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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.OtpEnabledTitle,
                content: context.HasDevice
                    ? NotificationTemplates.OtpEnabledContent(
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.OtpEnabledContentNoDevice(
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.Medium,
                metadata: metadata);
        }

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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SuccessfulLoginTitle,
                content: context.HasDevice
                    ? NotificationTemplates.SuccessfulLoginContent(
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.SuccessfulLoginContentNoDevice(
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.None,
                metadata: metadata);
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
            metadata["totpFailedAttempts"] = totpFailedAttempts.ToString();

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpFailedAttemptTitle,
                content: context.HasDevice
                    ? NotificationTemplates.TotpFailedAttemptContent(
                        totpFailedAttempts,
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.TotpFailedAttemptContentNoDevice(
                        totpFailedAttempts,
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.Medium,
                metadata: metadata);
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
            metadata["maxFailedAttempts"] = maxFailedAttempts.ToString();

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpLockoutTitle,
                content: context.HasDevice
                    ? NotificationTemplates.TotpLockoutContent(
                        maxFailedAttempts,
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.TotpLockoutContentNoDevice(
                        maxFailedAttempts,
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.High,
                metadata: metadata);
        }

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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.WebDavTokenResetTitle,
                content: context.HasDevice
                    ? NotificationTemplates.WebDavTokenResetContent(
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.WebDavTokenResetContentNoDevice(
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.Medium,
                metadata: metadata);
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SharedFileDownloadedTitle,
                content: context.HasDevice
                    ? NotificationTemplates.SharedFileDownloadedContent(
                        fileName,
                        context.Ip,
                        context.DeviceName,
                        context.Country,
                        context.Region,
                        context.City)
                    : NotificationTemplates.SharedFileDownloadedContentNoDevice(
                        fileName,
                        context.Ip,
                        context.Country,
                        context.Region,
                        context.City),
                priority: NotificationPriority.None,
                metadata: metadata);
        }

        public static async Task SendUploadHashMismatchNotificationAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string fileName,
            string proposedHash,
            string computedHash)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.UploadHashMismatchTitle,
                content: NotificationTemplates.UploadHashMismatchContent(
                    fileName,
                    proposedHash,
                    computedHash),
                priority: NotificationPriority.High,
                metadata: new Dictionary<string, string>
                {
                    ["fileName"] = fileName,
                    ["proposedHash"] = proposedHash,
                    ["computedHash"] = computedHash
                });
        }

        public static async Task SendStorageChunkMissingNotificationAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string fileName)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.StorageChunkMissingTitle,
                content: NotificationTemplates.StorageChunkMissingContent(fileName),
                priority: NotificationPriority.High,
                metadata: new Dictionary<string, string>
                {
                    ["fileName"] = fileName
                });
        }
    }
}
