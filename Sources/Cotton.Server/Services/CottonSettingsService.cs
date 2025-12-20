// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public class CottonSettingsService(CottonDbContext _dbContext)
    {
        const int defaultEncryptionThreads = 2;
        const int defaultMaxChunkSizeBytes = 64 * 1024 * 1024;
        const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;

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
    }
}
