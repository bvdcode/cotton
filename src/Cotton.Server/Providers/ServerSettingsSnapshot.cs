// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using System.Collections.Immutable;
using System.Net;

namespace Cotton.Server.Providers
{
    internal record ServerSettingsSnapshot
    {
        public required int EncryptionThreads { get; init; }

        public required int CipherChunkSizeBytes { get; init; }

        public required int CompressionLevel { get; init; }

        public required int MaxChunkSizeBytes { get; init; }

        public required int SessionTimeoutHours { get; init; }

        public required bool AllowCrossUserDeduplication { get; init; }

        public required bool AllowGlobalIndexing { get; init; }

        public required bool TelemetryEnabled { get; init; }

        public required bool DisableVersionCheck { get; init; }

        public required string Timezone { get; init; }

        public required Guid InstanceId { get; init; }

        public required string PublicBaseUrl { get; init; }

        public required IPAddress? TrustedProxyIpAddress { get; init; }

        public required byte? TrustedProxyPrefixLength { get; init; }

        public required string? SmtpServerAddress { get; init; }

        public required int? SmtpServerPort { get; init; }

        public required string? SmtpUsername { get; init; }

        public required string? SmtpPassword { get; init; }

        public required string? SmtpSenderEmail { get; init; }

        public required bool SmtpUseSsl { get; init; }

        public required string? S3AccessKeyId { get; init; }

        public required string? S3SecretAccessKey { get; init; }

        public required string? S3BucketName { get; init; }

        public required string? S3Region { get; init; }

        public required string? S3EndpointUrl { get; init; }

        public required EmailMode EmailMode { get; init; }

        public required ComputionMode ComputionMode { get; init; }

        public required StorageType StorageType { get; init; }

        public required ImmutableArray<ServerUsage> ServerUsage { get; init; }

        public required StorageSpaceMode StorageSpaceMode { get; init; }

        public required long? DefaultUserStorageQuotaBytes { get; init; }

        public required Guid? DefaultUserTemplateNodeId { get; init; }

        public required int TotpMaxFailedAttempts { get; init; }

        public required string? OidcClientId { get; init; }

        public required string? OidcClientSecret { get; init; }

        public required string? OidcIssuer { get; init; }

        public required string? CloudServicesToken { get; init; }

        public required GeoIpLookupMode GeoIpLookupMode { get; init; }

        public required string? CustomGeoIpLookupUrl { get; init; }

        public string GetInstanceIdHash()
        {
            return InstanceId.ToString().Sha256();
        }

        public TimeZoneInfo GetTimezoneInfo()
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(Timezone, out TimeZoneInfo? timezone)
                ? timezone
                : TimeZoneInfo.Utc;
        }

        internal static ServerSettingsSnapshot FromEntity(CottonServerSettings settings)
        {
            return new()
            {
                EncryptionThreads = settings.EncryptionThreads,
                CipherChunkSizeBytes = settings.CipherChunkSizeBytes,
                CompressionLevel = settings.CompressionLevel,
                MaxChunkSizeBytes = settings.MaxChunkSizeBytes,
                SessionTimeoutHours = settings.SessionTimeoutHours,
                AllowCrossUserDeduplication = settings.AllowCrossUserDeduplication,
                AllowGlobalIndexing = settings.AllowGlobalIndexing,
                TelemetryEnabled = settings.TelemetryEnabled,
                DisableVersionCheck = settings.DisableVersionCheck,
                Timezone = settings.Timezone,
                InstanceId = settings.InstanceId,
                PublicBaseUrl = settings.PublicBaseUrl,
                TrustedProxyIpAddress = settings.TrustedProxyIpAddress,
                TrustedProxyPrefixLength = settings.TrustedProxyPrefixLength,
                SmtpServerAddress = settings.SmtpServerAddress,
                SmtpServerPort = settings.SmtpServerPort,
                SmtpUsername = settings.SmtpUsername,
                SmtpPassword = settings.SmtpPasswordEncrypted,
                SmtpSenderEmail = settings.SmtpSenderEmail,
                SmtpUseSsl = settings.SmtpUseSsl,
                S3AccessKeyId = settings.S3AccessKeyId,
                S3SecretAccessKey = settings.S3SecretAccessKeyEncrypted,
                S3BucketName = settings.S3BucketName,
                S3Region = settings.S3Region,
                S3EndpointUrl = settings.S3EndpointUrl,
                EmailMode = settings.EmailMode,
                ComputionMode = settings.ComputionMode,
                StorageType = settings.StorageType,
                ServerUsage = ImmutableArray.CreateRange(settings.ServerUsage),
                StorageSpaceMode = settings.StorageSpaceMode,
                DefaultUserStorageQuotaBytes = settings.DefaultUserStorageQuotaBytes,
                DefaultUserTemplateNodeId = settings.DefaultUserTemplateNodeId,
                TotpMaxFailedAttempts = settings.TotpMaxFailedAttempts,
                OidcClientId = settings.OidcClientId,
                OidcClientSecret = settings.OidcClientSecretEncrypted,
                OidcIssuer = settings.OidcIssuer,
                CloudServicesToken = settings.CloudServicesTokenEncrypted,
                GeoIpLookupMode = settings.GeoIpLookupMode,
                CustomGeoIpLookupUrl = settings.CustomGeoIpLookupUrl,
            };
        }
    }
}
