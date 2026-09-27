// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;

namespace Cotton.Server.Handlers.Auth
{
    public record GetCurrentAuthUserQuery(Guid UserId) : IRequest<UserDto?>;

    public class GetCurrentAuthUserQueryHandler(
        CottonDbContext dbContext,
        IDatabaseIntegrityVerifier integrity) : IRequestHandler<GetCurrentAuthUserQuery, UserDto?>
    {
        public async Task<UserDto?> Handle(GetCurrentAuthUserQuery request, CancellationToken cancellationToken)
        {
            User? user = await dbContext.Users.FindAsync([request.UserId], cancellationToken);
            if (user is null)
            {
                return null;
            }

            integrity.RequireValid(dbContext, user, "auth.me");
            return user.Adapt<UserDto>();
        }
    }
}
