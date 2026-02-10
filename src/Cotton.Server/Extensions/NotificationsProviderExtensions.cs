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
            string username,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            GeoIpInfo ipInfo = await GeoIpHelpers.LookupAsync(ipAddress.ToString());
            string deviceName = device.FriendlyName ?? device.Type.ToString();

            await notifications.SendNotificationAsync(
                userId: Guid.Empty,
                title: NotificationTemplates.FailedLoginAttemptTitle,
                content: NotificationTemplates.FailedLoginAttemptContent(
                    username,
                    ipAddress.ToString(),
                    userAgent.ToString(),
                    deviceName,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.OtpEnabledTitle,
                content: NotificationTemplates.OtpEnabledContent(
                    ipAddress.ToString(),
                    userAgent.ToString(),
                    deviceName,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.SuccessfulLoginTitle,
                content: NotificationTemplates.SuccessfulLoginContent(
                    ipAddress.ToString(),
                    userAgent.ToString(),
                    deviceName,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpFailedAttemptTitle,
                content: NotificationTemplates.TotpFailedAttemptContent(
                    totpFailedAttempts,
                    ipAddress.ToString(),
                    userAgent.ToString(),
                    deviceName,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.TotpLockoutTitle,
                content: NotificationTemplates.TotpLockoutContent(
                    maxFailedAttempts,
                    ipAddress.ToString(),
                    userAgent.ToString(),
                    deviceName,
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

            await notifications.SendNotificationAsync(
                userId,
                title: NotificationTemplates.WebDavTokenResetTitle,
                content: NotificationTemplates.WebDavTokenResetContent(
                    ipAddress.ToString(),
                    userAgent.ToString(),
                    deviceName,
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
    }
}
