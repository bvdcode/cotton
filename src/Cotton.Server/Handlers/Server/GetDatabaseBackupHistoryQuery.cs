// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class GetDatabaseBackupHistoryQuery : IRequest<LatestDatabaseBackupDto[]>
    {
    }

    public class GetDatabaseBackupHistoryQueryHandler(IDatabaseBackupManifestService manifests)
        : IRequestHandler<GetDatabaseBackupHistoryQuery, LatestDatabaseBackupDto[]>
    {
        public async Task<LatestDatabaseBackupDto[]> Handle(
            GetDatabaseBackupHistoryQuery request, CancellationToken cancellationToken)
        {
            BackupManifestPointer? pointer = await manifests.ReadPointerAsync(cancellationToken);
            return pointer?.History?.Select(backup => backup.ToDto(pointer.UpdatedAtUtc)).ToArray() ?? [];
        }
    }
}
