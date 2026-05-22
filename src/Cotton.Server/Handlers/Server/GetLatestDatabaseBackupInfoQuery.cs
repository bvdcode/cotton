// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    /// <summary>
    /// Represents a get latest database backup info query sent through the mediator pipeline.
    /// </summary>
    public class GetLatestDatabaseBackupInfoQuery : IRequest<LatestDatabaseBackupDto?>
    {
    }

    /// <summary>
    /// Handles get latest database backup info queries in the mediator pipeline.
    /// </summary>
    public class GetLatestDatabaseBackupInfoQueryHandler(IDatabaseBackupManifestService _backupManifestService) : IRequestHandler<GetLatestDatabaseBackupInfoQuery, LatestDatabaseBackupDto?>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<LatestDatabaseBackupDto?> Handle(GetLatestDatabaseBackupInfoQuery request, CancellationToken cancellationToken)
        {
            ResolvedBackupManifest? backup = await _backupManifestService.TryGetLatestManifestAsync(cancellationToken);
            if (backup is null)
            {
                return null;
            }

            return new LatestDatabaseBackupDto
            {
                BackupId = backup.Manifest.BackupId,
                CreatedAtUtc = backup.Manifest.CreatedAtUtc,
                PointerUpdatedAtUtc = backup.Pointer.UpdatedAtUtc,
                DumpSizeBytes = backup.Manifest.DumpSizeBytes,
                ChunkCount = backup.Manifest.ChunkCount,
                DumpContentHash = backup.Manifest.DumpContentHash,
                SourceDatabase = backup.Manifest.SourceDatabase,
                SourceHost = backup.Manifest.SourceHost,
                SourcePort = backup.Manifest.SourcePort,
            };
        }
    }
}
