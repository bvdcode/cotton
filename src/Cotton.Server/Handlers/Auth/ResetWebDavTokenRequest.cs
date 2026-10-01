// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Providers;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Net;

namespace Cotton.Server.Handlers.Auth
{
    public record ResetWebDavTokenRequest(
        Guid UserId, IPAddress ClientIpAddress, string UserAgent) : IRequest<string>;

    public class ResetWebDavTokenRequestHandler(
        CottonDbContext dbContext,
        IPasswordHashService hasher,
        IDatabaseIntegrityVerifier integrity,
        WebDavAuthCache authCache,
        INotificationsProvider notifications,
        IGeoLookupService geoLookup,
        SettingsProvider settings,
        ILogger<ResetWebDavTokenRequestHandler> logger) : IRequestHandler<ResetWebDavTokenRequest, string>
    {
        public async Task<string> Handle(ResetWebDavTokenRequest request, CancellationToken cancellationToken)
        {
            User user = await dbContext.Users.FindAsync([request.UserId], cancellationToken)
                ?? throw new EntityNotFoundException<User>("Current user not found.");
            integrity.RequireValid(dbContext, user, "auth.webdav-token");
            string token = StringHelpers.CreateRandomString(AuthConstants.WebDavTokenLength);
            user.WebDavTokenPhc = hasher.Hash(token);
            await dbContext.SaveChangesAsync(cancellationToken);
            authCache.BumpUsernameCacheVersion(user.Username);
            await notifications.SendWebDavTokenResetAsync(
                geoLookup, settings, logger, request.UserId, request.ClientIpAddress, request.UserAgent);
            return token;
        }
    }
}
