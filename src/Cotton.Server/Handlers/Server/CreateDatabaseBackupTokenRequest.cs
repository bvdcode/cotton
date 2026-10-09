// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class CreateDatabaseBackupTokenRequest : IRequest<DatabaseBackupTokenDto>
    {
    }

    public class CreateDatabaseBackupTokenRequestHandler(DatabaseBackupTokenService tokens)
        : IRequestHandler<CreateDatabaseBackupTokenRequest, DatabaseBackupTokenDto>
    {
        public Task<DatabaseBackupTokenDto> Handle(CreateDatabaseBackupTokenRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DatabaseBackupTokenDto(tokens.Create()));
    }
}
