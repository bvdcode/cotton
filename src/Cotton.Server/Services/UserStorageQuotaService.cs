// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    // Logical quota is enforced when file references are created or changed.
    // Raw chunk uploads are intentionally handled by storage-pressure protection,
    // because chunking is an internal storage detail rather than user-owned data.
    /// <summary>
    /// Enforces logical per-user storage quotas for file references.
    /// </summary>
    /// <remarks>
    /// The hot upload path updates a small in-process cache instead of recomputing usage for every file.
    /// A cold cache still falls back to a single aggregate query, and chunk uploads are intentionally not
    /// billed as user data until they become reachable through a file reference.
    /// </remarks>
    public class UserStorageQuotaService(
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        UserStorageQuotaCache _usageCache,
        UserStorageQuotaMutationGate _mutationGate)
    {
        private readonly Dictionary<Guid, long> _scopedUsedBytesByUser = [];

        public ValueTask<IAsyncDisposable> EnterMutationAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _mutationGate.EnterAsync(userId, cancellationToken);
        }

        public async Task<long> GetUsedBytesAsync(Guid userId, CancellationToken ct = default)
        {
            if (_scopedUsedBytesByUser.TryGetValue(userId, out long cachedUsedBytes))
            {
                return cachedUsedBytes;
            }

            long resolvedUsedBytes = await GetProcessUsedBytesAsync(userId, ct);
            _scopedUsedBytesByUser[userId] = resolvedUsedBytes;
            return resolvedUsedBytes;
        }

        public async Task<UserStorageQuotaDto> GetSnapshotAsync(Guid userId, CancellationToken ct = default)
        {
            long usedBytes = await GetUsedBytesAsync(userId, ct);
            long? quotaBytes = _settings.GetServerSettings().DefaultUserStorageQuotaBytes;
            if (quotaBytes is null or <= 0)
            {
                return new UserStorageQuotaDto
                {
                    UsedBytes = usedBytes,
                    QuotaBytes = null,
                    AvailableBytes = null,
                };
            }

            return new UserStorageQuotaDto
            {
                UsedBytes = usedBytes,
                QuotaBytes = quotaBytes.Value,
                AvailableBytes = Math.Max(0, quotaBytes.Value - usedBytes),
            };
        }

        public async Task<long> EnsureCanAddFileReferenceAsync(
            Guid userId,
            Guid fileManifestId,
            CancellationToken ct = default)
        {
            long additionalBytes = await _dbContext.FileManifests
                .AsNoTracking()
                .Where(x => x.Id == fileManifestId)
                .Select(x => x.SizeBytes)
                .SingleAsync(ct);

            await EnsureCanAddLogicalBytesAsync(userId, additionalBytes, reserveInRequestState: true, ct);
            return Math.Max(0, additionalBytes);
        }

        public Task EnsureCanAddKnownFileSizeAsync(
            Guid userId,
            long sizeBytes,
            CancellationToken ct = default)
        {
            return EnsureCanAddLogicalBytesAsync(userId, sizeBytes, reserveInRequestState: false, ct);
        }

        public async Task<long> EnsureCanChangeFileManifestAsync(
            Guid userId,
            Guid nodeFileId,
            Guid newFileManifestId,
            CancellationToken ct = default)
        {
            var current = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => new
                {
                    x.FileManifestId,
                    x.FileManifest.SizeBytes,
                    x.FileManifest.ProposedContentHash
                })
                .SingleOrDefaultAsync(ct);
            if (current is null)
            {
                return 0;
            }

            var next = await _dbContext.FileManifests
                .AsNoTracking()
                .Where(x => x.Id == newFileManifestId)
                .Select(x => new
                {
                    x.SizeBytes,
                    x.ProposedContentHash
                })
                .SingleAsync(ct);

            long additionalBytes = current.FileManifestId == newFileManifestId
                || current.ProposedContentHash.SequenceEqual(next.ProposedContentHash)
                    ? 0
                    : next.SizeBytes;

            await EnsureCanAddLogicalBytesAsync(userId, additionalBytes, reserveInRequestState: true, ct);
            return Math.Max(0, additionalBytes);
        }

        private async Task EnsureCanAddLogicalBytesAsync(
            Guid userId,
            long additionalBytes,
            bool reserveInRequestState,
            CancellationToken ct)
        {
            long? quotaBytes = _settings.GetServerSettings().DefaultUserStorageQuotaBytes;
            if (quotaBytes is null or <= 0)
            {
                return;
            }

            long safeAdditionalBytes = Math.Max(0, additionalBytes);
            if (safeAdditionalBytes == 0)
            {
                return;
            }

            long usedBytes = reserveInRequestState
                ? await GetUsedBytesAsync(userId, ct)
                : await GetProcessUsedBytesAsync(userId, ct);
            if (usedBytes > quotaBytes.Value - safeAdditionalBytes)
            {
                throw new StorageQuotaExceededException<User>(
                    $"Storage quota exceeded. Current usage is {usedBytes} bytes, quota is {quotaBytes.Value} bytes, requested additional bytes is {safeAdditionalBytes}.",
                    new
                    {
                        UsedBytes = usedBytes,
                        QuotaBytes = quotaBytes.Value,
                        AdditionalBytes = safeAdditionalBytes,
                    });
            }

            if (reserveInRequestState)
            {
                _scopedUsedBytesByUser[userId] = usedBytes + safeAdditionalBytes;
            }
        }

        public void RecordLogicalBytesAdded(Guid userId, long bytes)
        {
            long safeBytes = Math.Max(0, bytes);
            if (safeBytes == 0)
            {
                return;
            }

            _usageCache.AddIfCached(userId, safeBytes);
        }

        public void RecordLogicalBytesRemoved(Guid userId, long bytes)
        {
            long safeBytes = Math.Max(0, bytes);
            if (safeBytes == 0)
            {
                return;
            }

            if (_scopedUsedBytesByUser.TryGetValue(userId, out long scopedUsedBytes))
            {
                long adjustedScopedUsedBytes = Math.Max(0, scopedUsedBytes - safeBytes);
                _scopedUsedBytesByUser[userId] = adjustedScopedUsedBytes;
            }

            _usageCache.RemoveIfCached(userId, safeBytes);
        }

        private async Task<long> LoadUsedBytesAsync(Guid userId, CancellationToken cancellationToken)
        {
            long? usedBytes = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .SumAsync(x => (long?)x.FileManifest.SizeBytes, cancellationToken);
            return usedBytes ?? 0;
        }

        private Task<long> GetProcessUsedBytesAsync(Guid userId, CancellationToken cancellationToken)
        {
            return _usageCache.GetOrLoadAsync(
                userId,
                ct => LoadUsedBytesAsync(userId, ct),
                cancellationToken);
        }
    }
}
