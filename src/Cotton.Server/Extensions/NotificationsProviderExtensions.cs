using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Helpers;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Primitives;
using System.Net;

namespace Cotton.Server.Extensions
{
    public static class NotificationsProviderExtensions
    {
        public static async Task SendFailedLoginAttemptAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string username,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId: userId,
                title: NotificationTemplates.FailedLoginAttemptTitle,
                content: hasDevice
                    ? NotificationTemplates.FailedLoginAttemptContent(
                        username,
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.FailedLoginAttemptContentNoDevice(
                        username,
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.High,
                metadata: new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
        }

        public static async Task SendOtpEnabledAsync(
            this INotificationsProvider notifications,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.OtpEnabledTitle,
                content: hasDevice
                    ? NotificationTemplates.OtpEnabledContent(
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.OtpEnabledContentNoDevice(
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.Medium,
                metadata: new Dictionary<string, string>
                {
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
        }

        public static async Task SendSuccessfulLoginAsync(
            this INotificationsProvider notifications,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SuccessfulLoginTitle,
                content: hasDevice
                    ? NotificationTemplates.SuccessfulLoginContent(
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.SuccessfulLoginContentNoDevice(
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.None,
                metadata: new Dictionary<string, string>
                {
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
        }

        public static async Task SendTotpFailedAttemptAsync(
            this INotificationsProvider notifications,
            Guid userId,
            int totpFailedAttempts,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpFailedAttemptTitle,
                content: hasDevice
                    ? NotificationTemplates.TotpFailedAttemptContent(
                        totpFailedAttempts,
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.TotpFailedAttemptContentNoDevice(
                        totpFailedAttempts,
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.Medium,
                metadata: new Dictionary<string, string>
                {
                    ["totpFailedAttempts"] = totpFailedAttempts.ToString(),
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
        }

        public static async Task SendTotpLockoutAsync(
            this INotificationsProvider notifications,
            Guid userId,
            int maxFailedAttempts,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpLockoutTitle,
                content: hasDevice
                    ? NotificationTemplates.TotpLockoutContent(
                        maxFailedAttempts,
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.TotpLockoutContentNoDevice(
                        maxFailedAttempts,
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.High,
                metadata: new Dictionary<string, string>
                {
                    ["maxFailedAttempts"] = maxFailedAttempts.ToString(),
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
        }

        public static async Task SendWebDavTokenResetAsync(
            this INotificationsProvider notifications,
            Guid userId,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.WebDavTokenResetTitle,
                content: hasDevice
                    ? NotificationTemplates.WebDavTokenResetContent(
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.WebDavTokenResetContentNoDevice(
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.Medium,
                metadata: new Dictionary<string, string>
                {
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
        }

        public static async Task SendSharedFileDownloadedNotificationAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string fileName,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            bool hasDevice = !string.IsNullOrWhiteSpace(deviceName)
                             && !string.Equals(deviceName, "Unknown", StringComparison.OrdinalIgnoreCase);

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SharedFileDownloadedTitle,
                content: hasDevice
                    ? NotificationTemplates.SharedFileDownloadedContent(
                        fileName,
                        ipAddress.ToString(),
                        deviceName,
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City)
                    : NotificationTemplates.SharedFileDownloadedContentNoDevice(
                        fileName,
                        ipAddress.ToString(),
                        ipInfo.Country,
                        ipInfo.Region,
                        ipInfo.City),
                priority: NotificationPriority.None,
                metadata: new Dictionary<string, string>
                {
                    ["fileName"] = fileName,
                    ["ip"] = ipAddress.ToString(),
                    ["userAgent"] = userAgent.ToString(),
                    ["device"] = deviceName,
                    ["country"] = ipInfo.Country,
                    ["region"] = ipInfo.Region,
                    ["city"] = ipInfo.City
                });
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
    }
}
