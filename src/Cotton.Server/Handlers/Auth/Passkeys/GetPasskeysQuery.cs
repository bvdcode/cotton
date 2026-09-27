// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.Passkeys;
using EasyExtensions.AspNetCore.Exceptions;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Passkeys
{
    public record GetPasskeysQuery(Guid UserId) : IRequest<IReadOnlyList<PasskeyCredentialDto>>;

    public class GetPasskeysQueryHandler(
        CottonDbContext _dbContext)
        : IRequestHandler<GetPasskeysQuery, IReadOnlyList<PasskeyCredentialDto>>
    {
        public async Task<IReadOnlyList<PasskeyCredentialDto>> Handle(GetPasskeysQuery command, CancellationToken ct)
        {
            Guid userId = command.UserId;
            List<PasskeyCredentialDto> credentials = await _dbContext.UserPasskeyCredentials
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastUsedAt ?? x.CreatedAt)
                .Select(x => new PasskeyCredentialDto
                {
                    Id = x.Id,
                    Label = x.Label,
                    CredentialId = WebEncoders.Base64UrlEncode(x.CredentialId),
                    Transports = x.Transports,
                    AaGuid = x.AaGuid,
                    IsBackupEligible = x.IsBackupEligible,
                    IsBackedUp = x.IsBackedUp,
                    CreatedAt = x.CreatedAt,
                    LastUsedAt = x.LastUsedAt
                })
                .ToListAsync(ct);

            foreach (PasskeyCredentialDto credential in credentials)
            {
                credential.AuthenticatorName = PasskeyAuthenticatorResolver.ResolveName(credential.AaGuid);
                credential.AuthenticatorKind = PasskeyAuthenticatorResolver.ResolveKind(credential.Transports);
            }

            return credentials;
        }
    }
}
