// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Localization;
using Cotton.Models.Enums;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Microsoft.Extensions.Primitives;
using System.Net;

namespace Cotton.Server.Extensions
{
    public static class StorageNotificationExtensions
    {
        public static async Task SendSharedFileDownloadedNotificationAsync(
            this INotificationsProvider notifications,
            IGeoLookupService geoLookup,
            Guid userId,
            string fileName,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            NotificationClientContext context = await NotificationClientContextFactory.CreateAsync(
                geoLookup,
                ipAddress,
                userAgent);
            Dictionary<string, string> metadata = NotificationClientContextFactory.CreateMetadata(context);
            metadata["fileName"] = fileName;
            string contentKey = context.HasDevice
                ? NotificationTemplateKeys.SharedFileDownloadedWithDeviceContent
                : NotificationTemplateKeys.SharedFileDownloadedWithoutDeviceContent;

            await notifications.SendNotificationAsync(
                userId,
                NotificationTemplates.SharedFileDownloadedTitle,
                context.HasDevice
                    ? NotificationTemplates.SharedFileDownloadedContent(fileName, null, context.DeviceName, context.Location)
                    : NotificationTemplates.SharedFileDownloadedContentNoDevice(fileName, null, context.Location),
                NotificationPriority.None,
                NotificationTemplateMetadata.Create(
                    NotificationTemplateKeys.SharedFileDownloadedTitle,
                    contentKey,
                    metadata));
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
                ["computedTail"] = NotificationTemplates.FormatHashTail(computedHash),
            };

            await notifications.SendNotificationAsync(
                userId,
                NotificationTemplates.UploadHashMismatchTitle,
                NotificationTemplates.UploadHashMismatchContent(fileName, proposedHash, computedHash),
                NotificationPriority.High,
                NotificationTemplateMetadata.Create(
                    NotificationTemplateKeys.UploadHashMismatchTitle,
                    NotificationTemplateKeys.UploadHashMismatchContent,
                    metadata));
        }

        public static async Task SendStorageChunkMissingNotificationAsync(
            this INotificationsProvider notifications,
            Guid userId,
            string fileName)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            Dictionary<string, string> metadata = new()
            {
                ["fileName"] = fileName,
            };
            await notifications.SendNotificationAsync(
                userId,
                NotificationTemplates.StorageChunkMissingTitle,
                NotificationTemplates.StorageChunkMissingContent(fileName),
                NotificationPriority.High,
                NotificationTemplateMetadata.Create(
                    NotificationTemplateKeys.StorageChunkMissingTitle,
                    NotificationTemplateKeys.StorageChunkMissingContent,
                    metadata));
        }
    }
}
