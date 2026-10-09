// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class GetLatestDatabaseBackupInfoQuery : IRequest<LatestDatabaseBackupDto?>
    {
    }

    public class GetLatestDatabaseBackupInfoQueryHandler(IDatabaseBackupManifestService _backupManifestService) : IRequestHandler<GetLatestDatabaseBackupInfoQuery, LatestDatabaseBackupDto?>
    {
        public async Task<LatestDatabaseBackupDto?> Handle(GetLatestDatabaseBackupInfoQuery request, CancellationToken cancellationToken)
        {
            ResolvedBackupManifest? backup = await _backupManifestService.TryGetLatestManifestAsync(cancellationToken);
            if (backup is null)
            {
                return null;
            }

            return BackupManifestReference.FromManifest(backup.ManifestStorageKey, backup.Manifest)
                .ToDto(backup.Pointer.UpdatedAtUtc);
        }
    }
}
