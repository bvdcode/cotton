// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record CreateFileDownloadTokenRequest(Guid UserId, Guid NodeFileId, int ExpireAfterMinutes)
        : IRequest<string?>;

    public class CreateFileDownloadTokenRequestHandler(
        CottonDbContext database,
        FileDownloadTokenService tokens)
        : IRequestHandler<CreateFileDownloadTokenRequest, string?>
    {
        public async Task<string?> Handle(CreateFileDownloadTokenRequest request, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ExpireAfterMinutes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.ExpireAfterMinutes, ShareLinkExpiration.MaximumMinutes);
            bool exists = await database.NodeFiles.AnyAsync(file => file.Id == request.NodeFileId
                && file.OwnerId == request.UserId && file.Node.Type == NodeType.Default, ct);
            if (!exists)
            {
                return null;
            }

            string token = tokens.Create(request.UserId, request.NodeFileId,
                TimeSpan.FromMinutes(request.ExpireAfterMinutes));
            return FileDownloadToken.BuildUrl(request.NodeFileId, token);
        }
    }
}
