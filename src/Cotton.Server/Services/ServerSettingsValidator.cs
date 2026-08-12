// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Cotton.Server.Services
{
    public class ServerSettingsValidator(
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        CottonPublicEmailProvider _publicEmailProvider,
        S3ConfigurationValidator _s3Validator)
    {
        public string? ValidateTimezone(string? timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
            {
                return "Timezone must be provided.";
            }

            return TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _)
                ? null
                : "Timezone not found: " + timezone;
        }

        public string? ValidateTelemetryChange(bool enabled)
        {
            if (enabled)
            {
                return null;
            }

            ServerSettingsSnapshot settings = _settingsProvider.GetServerSettings();
            if (settings.EmailMode == EmailMode.Cloud)
            {
                return "Telemetry must be enabled to use Cotton Bridge Mail.";
            }

            if (settings.ComputionMode == ComputionMode.Cloud)
            {
                return "Telemetry must be enabled to use Cotton Bridge AI.";
            }

            if (settings.GeoIpLookupMode == GeoIpLookupMode.CottonCloud)
            {
                return "Telemetry must be enabled to use Cotton Bridge IP lookup.";
            }

            return null;
        }

        public async Task<string?> ValidateEmailModeAsync(
            EmailMode mode,
            CancellationToken cancellationToken = default)
        {
            ServerSettingsSnapshot settings = _settingsProvider.GetServerSettings();
            if (mode == EmailMode.Cloud)
            {
                if (!settings.TelemetryEnabled)
                {
                    return "Telemetry must be enabled to use Cotton Bridge Mail.";
                }

                bool isHealthy = await _publicEmailProvider.CheckHealthAsync(cancellationToken);
                return isHealthy
                    ? null
                    : "Cotton Bridge Mail is currently unavailable. Please try again later or switch to Custom email service.";
            }

            if (mode == EmailMode.Custom)
            {
                return IsEmailConfigComplete(settings)
                    ? null
                    : "SMTP settings must be configured before enabling Custom email service.";
            }

            if (mode == EmailMode.None)
            {
                return null;
            }

            return "Invalid email mode: " + mode;
        }

        public string? ValidateComputionMode(ComputionMode mode)
        {
            if (mode == ComputionMode.Cloud && !_settingsProvider.GetServerSettings().TelemetryEnabled)
            {
                return "Telemetry must be enabled to use Cotton Bridge AI.";
            }

            return Enum.IsDefined(mode)
                ? null
                : "Invalid computation mode: " + mode;
        }

        public string? ValidateGeoIpLookupMode(GeoIpLookupMode mode)
        {
            ServerSettingsSnapshot settings = _settingsProvider.GetServerSettings();
            if (mode == GeoIpLookupMode.CottonCloud && !settings.TelemetryEnabled)
            {
                return "Telemetry must be enabled to use Cotton Bridge IP lookup.";
            }

            if (mode == GeoIpLookupMode.CustomHttp && string.IsNullOrWhiteSpace(settings.CustomGeoIpLookupUrl))
            {
                return "Custom GeoIP lookup URL must be configured before enabling Custom HTTP lookup.";
            }

            if (mode == GeoIpLookupMode.MaxMindLocal)
            {
                return "MaxMind local lookup is not configurable yet.";
            }

            return Enum.IsDefined(mode)
                ? null
                : "Invalid GeoIP lookup mode: " + mode;
        }

        public async Task<string?> ValidateStorageTypeAsync(
            StorageType type,
            CancellationToken cancellationToken = default)
        {
            if (type == StorageType.Local)
            {
                return null;
            }

            if (type != StorageType.S3)
            {
                return "Invalid storage type: " + type;
            }

            ServerSettingsSnapshot settings = _settingsProvider.GetServerSettings();
            S3Config s3Config = new()
            {
                AccessKey = settings.S3AccessKeyId ?? string.Empty,
                SecretKey = settings.S3SecretAccessKey ?? string.Empty,
                Endpoint = settings.S3EndpointUrl ?? string.Empty,
                Region = settings.S3Region ?? string.Empty,
                Bucket = settings.S3BucketName ?? string.Empty
            };

            string? configError = S3ConfigurationValidator.ValidateShape(s3Config);
            if (configError is not null)
            {
                return "S3 settings must be configured before enabling S3 storage.";
            }

            return await _s3Validator.ValidateAsync(s3Config, cancellationToken);
        }

        public async Task<string?> ValidateS3ConfigAsync(
            S3Config? s3Config,
            CancellationToken cancellationToken = default)
        {
            return await _s3Validator.ValidateAsync(s3Config, cancellationToken);
        }

        public string? ValidateEmailConfig(EmailConfig? emailConfig)
        {
            if (emailConfig is null)
            {
                return "SMTP settings must be provided.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.SmtpServer))
            {
                return "SMTP server must be provided.";
            }

            if (!TryParsePort(emailConfig.Port, out _))
            {
                return "SMTP port must be a number between 1 and 65535.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.Username))
            {
                return "SMTP username must be provided.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.Password))
            {
                return "SMTP password must be provided.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.FromAddress))
            {
                return "SMTP sender address must be provided.";
            }

            try
            {
                _ = new MailAddress(emailConfig.FromAddress);
            }
            catch (FormatException)
            {
                return "SMTP sender address must be a valid email address.";
            }

            return null;
        }

        public string? ValidateDefaultUserStorageQuotaBytes(long? quotaBytes)
        {
            if (quotaBytes is null or 0)
            {
                return null;
            }

            return quotaBytes > 0
                ? null
                : "Default user storage quota must be zero, empty, or a positive byte value.";
        }

        public async Task<string?> ValidateDefaultUserTemplateNodeIdAsync(
            Guid? nodeId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            if (nodeId is null || nodeId == Guid.Empty)
            {
                return null;
            }

            bool exists = await _dbContext.Nodes
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == nodeId.Value
                    && x.OwnerId == ownerId
                    && x.Type == NodeType.Default,
                    cancellationToken);

            return exists
                ? null
                : "Default user template folder was not found.";
        }

        public string? ValidatePublicBaseUrl(string? url)
        {
            return SettingsProvider.TryNormalizePublicBaseUrl(url, out _)
                ? null
                : "Public base URL must be an absolute HTTP or HTTPS URL.";
        }

        public string? ValidateCustomGeoIpLookupUrl(string? url)
        {
            return SettingsProvider.TryNormalizePublicBaseUrl(url, out _)
                ? null
                : "Custom GeoIP lookup URL must be an absolute HTTP or HTTPS URL.";
        }

        public static bool TryParsePort(string? value, out int port)
        {
            return int.TryParse(value, out port) && port is >= 1 and <= 65535;
        }

        private static bool IsEmailConfigComplete(ServerSettingsSnapshot settings)
        {
            return !string.IsNullOrWhiteSpace(settings.SmtpServerAddress)
                && settings.SmtpServerPort is >= 1 and <= 65535
                && !string.IsNullOrWhiteSpace(settings.SmtpUsername)
                && !string.IsNullOrWhiteSpace(settings.SmtpPassword)
                && !string.IsNullOrWhiteSpace(settings.SmtpSenderEmail);
        }
    }
}
