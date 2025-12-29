// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class SettingsProvider(
        IStreamCipher _crypto,
        CottonDbContext _dbContext)
    {
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 64 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;
        public CottonServerSettings GetServerSettings()
        {
            var settings = _dbContext.ServerSettings
                .AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();
            if (settings is not null)
            {
                return settings;
            }
            return new()
            {
                AllowCrossUserDeduplication = false,
                AllowGlobalIndexing = false,
                CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                EncryptionThreads = defaultEncryptionThreads,
                MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                SessionTimeoutHours = 24 * 30,
                TelemetryEnabled = false,
                Timezone = "America/Los_Angeles"
            };
        }

        public Task<bool> IsServerInitializedAsync()
        {
            return _dbContext.ServerSettings.AnyAsync();
        }

        public Task<bool> ServerHasUsersAsync()
        {
            return _dbContext.Users.AnyAsync();
        }

        public async Task<string?> ValidateServerSettingsAsync(ServerSettingsRequestDto request)
        {
            bool tzExists = TimeZoneInfo.TryFindSystemTimeZoneById(request.Timezone, out var _);
            if (!tzExists)
            {
                return "Timezone not found: " + request.Timezone;
            }
            if (!request.Telemetry)
            {
                if (request.Email == EmailMode.Cloud)
                {
                    return "Telemetry must be enabled to use cloud email service.";
                }
                if (request.ComputionMode == ComputionMode.Cloud)
                {
                    return "Telemetry must be enabled to use cloud AI service.";
                }
            }
            if (request.Email == EmailMode.Custom)
            {
                if (request.EmailConfig is null)
                {
                    return "EmailConfig must be provided when using Custom email service.";
                }
            }
            if (request.Storage == StorageType.S3)
            {
                if (request.S3Config is null)
                {
                    return "S3Config must be provided when using S3 storage.";
                }
            }
            if (request.ImportSource != ImportSource.None)
            {
                if (request.ImportSource == ImportSource.Webdav && request.WebdavConfig is null)
                {
                    return "WebdavConfig must be provided when using Webdav import source.";
                }
            }
            return null;
        }

        public async Task SaveServerSettingsAsync(ServerSettingsRequestDto request)
        {
            int? smtpPort = null;
            if (request.EmailConfig?.Port is not null)
            {
                bool parsed = int.TryParse(request.EmailConfig.Port, out int port);
                if (parsed)
                {
                    smtpPort = port;
                }
            }
            var lastSettings = await _dbContext.ServerSettings
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
            Guid instanceId = lastSettings?.InstanceId ?? Guid.NewGuid();
            CottonServerSettings newSettings = new()
            {
                EmailMode = request.Email,
                ComputionMode = request.ComputionMode,
                StorageType = request.Storage,
                ImportSource = request.ImportSource,
                EncryptionThreads = defaultEncryptionThreads,
                MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                SessionTimeoutHours = 24 * 30,
                AllowCrossUserDeduplication = request.TrustedMode,
                AllowGlobalIndexing = request.TrustedMode,
                TelemetryEnabled = request.Telemetry,
                Timezone = request.Timezone,
                SmtpServerAddress = request.EmailConfig?.SmtpServer,
                SmtpServerPort = smtpPort,
                SmtpUsername = request.EmailConfig?.Username,
                SmtpPasswordEncrypted = TryEncrypt(request.EmailConfig?.Password),
                SmtpSenderEmail = request.EmailConfig?.FromAddress,
                SmtpUseSsl = request.EmailConfig?.UseSSL ?? false,
                S3AccessKeyId = request.S3Config?.AccessKey,
                S3SecretAccessKeyEncrypted = TryEncrypt(request.S3Config?.SecretKey),
                S3BucketName = request.S3Config?.Bucket,
                S3Region = request.S3Config?.Region,
                S3EndpointUrl = request.S3Config?.Endpoint,
                InstanceId = instanceId,
                ServerUsage = request.Usage,
                StorageSpaceMode = request.StorageSpace,
                WebdavHost = request.WebdavConfig?.ServerUrl,
                WebdavUsername = request.WebdavConfig?.Username,
                WebdavPasswordEncrypted = TryEncrypt(request.WebdavConfig?.Password)
            };
            await _dbContext.ServerSettings.AddAsync(newSettings);
            await _dbContext.SaveChangesAsync();
        }

        private string? TryEncrypt(string? password)
        {
            if (password is null)
            {
                return null;
            }
            byte[] passwordBytes = _crypto.Encrypt(password);
            return Convert.ToBase64String(passwordBytes);
        }
    }
}
