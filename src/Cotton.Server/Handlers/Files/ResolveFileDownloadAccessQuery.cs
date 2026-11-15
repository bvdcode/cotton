// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record ResolveFileDownloadAccessQuery(Guid NodeFileId, FileDownloadCredentials Credentials)
        : IRequest<FileDownloadAccess?>;

    public class ResolveFileDownloadAccessQueryHandler(
        CottonDbContext database,
        FileDownloadTokenService tokens,
        IDatabaseIntegrityVerifier integrity)
        : IRequestHandler<ResolveFileDownloadAccessQuery, FileDownloadAccess?>
    {
        public async Task<FileDownloadAccess?> Handle(ResolveFileDownloadAccessQuery request, CancellationToken ct)
        {
            FileDownloadCredentials credentials = request.Credentials;
            if (!credentials.IsValid)
            {
                return null;
            }

            if (credentials.DownloadToken is string downloadToken)
            {
                if (!tokens.TryRead(downloadToken, out FileDownloadToken? download)
                    || download.NodeFileId != request.NodeFileId)
                {
                    return null;
                }
                return new FileDownloadAccess(download.UserId);
            }

            DownloadToken? shareToken = await database.DownloadTokens
                .FindActiveAsync(credentials.Token!, request.NodeFileId, ct);
            if (shareToken is null)
            {
                return null;
            }
            integrity.RequireValid(database, shareToken, "file.download-token");
            return new FileDownloadAccess(shareToken.CreatedByUserId, shareToken);
        }
    }
}
