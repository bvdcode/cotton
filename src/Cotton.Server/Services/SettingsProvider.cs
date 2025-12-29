// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class SettingsProvider(CottonDbContext _dbContext)
    {
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 64 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;

        //[nameof(CottonServerSettings.EncryptionThreads)] = defaultEncryptionThreads.ToString(),
        //[nameof(CottonServerSettings.MaxChunkSizeBytes)] = defaultMaxChunkSizeBytes.ToString(),
        //[nameof(CottonServerSettings.CipherChunkSizeBytes)] = defaultCipherChunkSizeBytes.ToString(),
        public CottonServerSettings GetServerSettings()
        {
            return new()
            {
                AllowCrossUserDeduplication = false,
                AllowGlobalIndexing = false,
                CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                EncryptionThreads = defaultEncryptionThreads,
                MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                SessionTimeoutHours = 24 * 30,
                TelemetryEnabled = true,
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
            if (!request.Telemetry)
            {
                if (request.Email == Database.Models.Enums.EmailMode.Cloud)
                {
                    return "Telemetry must be enabled to use cloud email service.";
                }
                if (request.Ai == Database.Models.Enums.ComputionMode.Cloud)
                {
                    return "Telemetry must be enabled to use cloud AI service.";
                }
            }
            if (request.Email == EmailMode.Cloud)
            {
                if (request.EmailConfig is null)
                {
                    return "EmailConfig must be provided when using cloud email service.";
                }
            }
            if (request.Storage == StorageType.S3)
            {
                if (request.S3Config is null)
                {
                    return "S3Config must be provided when using S3 storage.";
                }
            }
            if (request.ImportSources.Length > 0)
            {
                foreach (var source in request.ImportSources)
                {
                    if (source == ImportSource.Nextcloud && request.NextcloudConfig is null)
                    {
                        return "NextcloudConfig must be provided when using Nextcloud import source.";
                    }
                    if (source == ImportSource.Webdav && request.WebdavConfig is null)
                    {
                        return "WebdavConfig must be provided when using Webdav import source.";
                    }
                }
            }
            return null;
        }

        public async Task SaveServerSettingsAsync(ServerSettingsRequestDto request)
        {

        }
    }
}
