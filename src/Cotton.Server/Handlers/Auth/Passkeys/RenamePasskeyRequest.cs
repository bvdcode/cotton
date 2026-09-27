// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Passkeys
{
    public record RenamePasskeyRequest(Guid UserId, Guid CredentialId, string? Label) : IRequest<PasskeyCredentialDto>;

    public class RenamePasskeyRequestHandler(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity)
        : IRequestHandler<RenamePasskeyRequest, PasskeyCredentialDto>
    {
        public async Task<PasskeyCredentialDto> Handle(RenamePasskeyRequest command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            Guid credentialId = command.CredentialId;
            string? label = command.Label;
            UserPasskeyCredential credential = await _dbContext.UserPasskeyCredentials
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == credentialId, ct)
                ?? throw new EntityNotFoundException<UserPasskeyCredential>("Passkey not found.");
            _integrity.RequireValid(_dbContext, credential, "passkey.rename");

            credential.Label = PasskeyLabelNormalizer.Normalize(label);
            await _dbContext.SaveChangesAsync(ct);
            return PasskeyProtocolMapper.ToDto(credential);
        }
    }
}
