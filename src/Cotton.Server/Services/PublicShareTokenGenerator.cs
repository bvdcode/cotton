// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Generates tokens for public file and folder share links.
    /// </summary>
    public class PublicShareTokenGenerator(CottonDbContext _dbContext)
    {
        /// <summary>
        /// Maximum number of active public links that use compact tokens.
        /// </summary>
        public const int CompactTokenActiveShareLimit = 1_000;

        private const int CompactTokenLength = 8;
        internal const int ExpandedTokenLength = 12;
        private const int MaxGenerationAttempts = 8;
        private const string CompactAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        private const string ExpandedAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        /// <summary>
        /// Creates a unique token sized for the current number of active public links.
        /// </summary>
        public async Task<string> CreateUniqueAsync(CancellationToken cancellationToken = default)
        {
            DateTime now = DateTime.UtcNow;
            int activeDownloadTokenCount = await _dbContext.DownloadTokens
                .CountAsync(x => x.ExpiresAt == null || x.ExpiresAt > now, cancellationToken);
            int activeNodeShareTokenCount = await _dbContext.NodeShareTokens
                .CountAsync(x => x.ExpiresAt == null || x.ExpiresAt > now, cancellationToken);
            int activeShareCount = checked(activeDownloadTokenCount + activeNodeShareTokenCount);

            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                string candidate = CreateForActiveShareCount(activeShareCount);
                bool existsInDownloadTokens = await _dbContext.DownloadTokens
                    .AnyAsync(x => x.Token == candidate, cancellationToken);
                if (existsInDownloadTokens)
                {
                    continue;
                }

                bool existsInNodeShareTokens = await _dbContext.NodeShareTokens
                    .AnyAsync(x => x.Token == candidate, cancellationToken);
                if (!existsInNodeShareTokens)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Unable to generate a unique share token.");
        }

        internal static string CreateForActiveShareCount(int activeShareCount)
        {
            if (activeShareCount >= CompactTokenActiveShareLimit)
            {
                return RandomNumberGenerator.GetString(ExpandedAlphabet, ExpandedTokenLength);
            }

            return RandomNumberGenerator.GetString(CompactAlphabet, CompactTokenLength);
        }
    }
}
