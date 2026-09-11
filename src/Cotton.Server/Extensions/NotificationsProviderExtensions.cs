// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Net;

namespace Cotton.Server.Extensions
{
    public static class NotificationsProviderExtensions
    {
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

        public static async Task SendFailedLoginAttemptAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            Guid userId,
            string username,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
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
                metadata: NotificationTemplateMetadata.Create(NotificationTemplateKeys.FailedLoginAttemptTitle, contentKey, metadata));
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

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
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
                NotificationTemplateMetadata.Create(NotificationTemplateKeys.OtpDisabledTitle, contentKey, metadata));
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

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
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
                NotificationTemplateMetadata.Create(NotificationTemplateKeys.OtpEnabledTitle, contentKey, metadata));
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

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
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
                NotificationTemplateMetadata.Create(NotificationTemplateKeys.SuccessfulLoginTitle, contentKey, metadata));
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

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
            metadata["totpFailedAttempts"] = NotificationClientContextFactory.FormatInvariant(totpFailedAttempts);
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
                metadata: NotificationTemplateMetadata.Create(NotificationTemplateKeys.TotpFailedAttemptTitle, contentKey, metadata));
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

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
            metadata["maxFailedAttempts"] = NotificationClientContextFactory.FormatInvariant(maxFailedAttempts);
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
                metadata: NotificationTemplateMetadata.Create(NotificationTemplateKeys.TotpLockoutTitle, contentKey, metadata));
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

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(geoLookup, ipAddress, userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
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
                NotificationTemplateMetadata.Create(NotificationTemplateKeys.WebDavTokenResetTitle, contentKey, metadata));
        }

    }
}
