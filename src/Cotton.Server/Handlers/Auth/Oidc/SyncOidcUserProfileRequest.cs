// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.Oidc
{
    internal record SyncOidcUserProfileRequest(User User, OidcProvider Provider, OidcIdentityClaims Claims) : IRequest;

    internal class SyncOidcUserProfileRequestHandler(
        OidcAvatarImportService _avatarImporter)
        : IRequestHandler<SyncOidcUserProfileRequest>
    {
        public async Task Handle(SyncOidcUserProfileRequest command, CancellationToken ct)
        {
            User user = command.User;
            OidcProvider provider = command.Provider;
            OidcIdentityClaims claims = command.Claims;
            OidcIdentityMapper.ApplyProfile(user, provider, claims);
            if (!provider.SyncAvatar)
            {
                return;
            }

            await _avatarImporter.TryImportMissingAvatarAsync(
                user,
                claims.PictureUrl,
                provider.Issuer,
                ct);
        }
    }
}
