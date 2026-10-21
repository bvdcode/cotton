// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Contains explicitly approved database mutations that bypass change tracking.
    /// </summary>
    /// <remarks>
    /// No method or other member may be added, and no existing operation may be changed,
    /// without two separate, explicit confirmations from the requesting user for that exact change.
    /// Every mutation must have a behavioral test proving that its updated fields are excluded
    /// from the actual integrity descriptor and that existing signatures remain valid.
    /// Deletion tests must prove the exact scope and that retained rows and signatures remain unchanged.
    /// The source approval test must not be updated to admit a change before both confirmations.
    /// </remarks>
#pragma warning disable EEX0015
    internal static class ApprovedBulkMutationExtensions
    {
        public static Task<int> DeleteFileEmbeddingsAsync(
            this DbSet<FileEmbedding> embeddings,
            IReadOnlyCollection<Guid> manifestIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(embeddings);
            ArgumentNullException.ThrowIfNull(manifestIds);

            return embeddings
                .Where(embedding => manifestIds.Contains(embedding.FileManifestId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteFileManifestChunksAsync(
            this DbSet<FileManifestChunk> manifestChunks,
            IReadOnlyCollection<Guid> manifestIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(manifestChunks);
            ArgumentNullException.ThrowIfNull(manifestIds);

            return manifestChunks
                .Where(chunk => manifestIds.Contains(chunk.FileManifestId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteChunkOwnershipsAsync(
            this DbSet<ChunkOwnership> ownerships,
            IReadOnlyCollection<byte[]> chunkHashes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ownerships);
            ArgumentNullException.ThrowIfNull(chunkHashes);

            return ownerships
                .Where(ownership => chunkHashes.Contains(ownership.ChunkHash))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteExpiredSyncChangesAsync(
            this DbSet<SyncChange> changes,
            DateTime cutoff,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(changes);

            return changes
                .Where(change => change.CreatedAt < cutoff)
                .Where(change => changes.Any(newerExpired =>
                    newerExpired.OwnerId == change.OwnerId
                    && newerExpired.CreatedAt < cutoff
                    && newerExpired.Id > change.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteChunksAsync(
            this DbSet<Chunk> rows, IReadOnlyCollection<byte[]> hashes, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(hashes);

            return rows.Where(item => hashes.Contains(item.Hash)).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteNodeShareTokensForNodesAsync(
            this DbSet<NodeShareToken> rows, IReadOnlyCollection<Guid> nodeIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(nodeIds);

            return rows.Where(item => nodeIds.Contains(item.NodeId)).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteDownloadTokensForFilesAsync(
            this DbSet<DownloadToken> rows, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(fileIds);

            return rows.Where(item => fileIds.Contains(item.NodeFileId)).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteUserDownloadTokensForFileAsync(
            this DbSet<DownloadToken> rows, Guid userId, Guid fileId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.CreatedByUserId == userId && item.NodeFileId == fileId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteUserDownloadTokensForNodesAsync(
            this DbSet<DownloadToken> rows, Guid userId, IReadOnlyCollection<Guid> nodeIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(nodeIds);

            return rows.Where(item => item.CreatedByUserId == userId && nodeIds.Contains(item.NodeFile.NodeId)).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteFileEmbeddingsAsync(
            this DbSet<FileEmbedding> rows, Guid manifestId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.FileManifestId == manifestId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteUserAsync(
            this DbSet<User> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.Id == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteDownloadTokensForUserAsync(
            this DbSet<DownloadToken> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.CreatedByUserId == userId || item.NodeFile.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteNodeShareTokensForUserAsync(
            this DbSet<NodeShareToken> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.CreatedByUserId == userId || item.Node.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteNotificationsForUserAsync(
            this DbSet<Notification> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeletePasskeysForUserAsync(
            this DbSet<UserPasskeyCredential> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteExternalIdentitiesForUserAsync(
            this DbSet<UserExternalIdentity> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteOidcStatesForUserAsync(
            this DbSet<OidcLoginState> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.LinkUserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteRefreshTokensForUserAsync(
            this DbSet<ExtendedRefreshToken> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteSyncChangesForUserAsync(
            this DbSet<SyncChange> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteChunkOwnershipsForUserAsync(
            this DbSet<ChunkOwnership> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteNodeFilesForUserAsync(
            this DbSet<NodeFile> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteLeafNodesForUserAsync(
            this DbSet<Node> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.OwnerId == userId && !item.Children.Any()).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteLayoutsForUserAsync(
            this DbSet<Layout> rows, Guid userId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.OwnerId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteFileEmbeddingsByIdAsync(
            this DbSet<FileEmbedding> rows, IReadOnlyCollection<Guid> embeddingIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(embeddingIds);

            return rows.Where(item => embeddingIds.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteOrphanedFileManifestsAsync(
            this DbSet<FileManifest> rows, IReadOnlyCollection<Guid> manifestIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(manifestIds);

            return rows.Where(item => manifestIds.Contains(item.Id) && !item.NodeFiles.Any()).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteOidcStatesForProviderAsync(
            this DbSet<OidcLoginState> rows, Guid providerId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.ProviderId == providerId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteExternalIdentitiesForProviderAsync(
            this DbSet<UserExternalIdentity> rows, Guid providerId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.ProviderId == providerId).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> DeleteExpiredOidcStatesAsync(
            this DbSet<OidcLoginState> rows, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows.Where(item => item.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync(cancellationToken);
        }

        public static Task<int> SetGarbageCollectionScheduleAsync(
            this IQueryable<Chunk> query,
            DateTime? deleteAfter,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (deleteAfter.HasValue)
            {
                query = query.Where(chunk => chunk.GCScheduledAfter == null);
            }
            else
            {
                query = query.Where(chunk => chunk.GCScheduledAfter != null);
            }

            return query.ExecuteUpdateAsync(
                update => update.SetProperty(chunk => chunk.GCScheduledAfter, deleteAfter),
                cancellationToken);
        }
    }
#pragma warning restore EEX0015
}
