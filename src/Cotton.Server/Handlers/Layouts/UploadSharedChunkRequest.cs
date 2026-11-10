// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record UploadSharedChunkRequest(string Token, Guid NodeId, Guid SessionId,
        string Hash, Stream Content, long Length) : IRequest<bool>;

    public class UploadSharedChunkRequestHandler(IMediator mediator, IChunkIngestService ingest,
        SharedUploadSessionCache sessions, SettingsProvider settings) : IRequestHandler<UploadSharedChunkRequest, bool>
    {
        public async Task<bool> Handle(UploadSharedChunkRequest request, CancellationToken ct)
        {
            SharedNodeAccess access = await mediator.Send(new RequireSharedUploadAccessQuery(request.Token, request.NodeId), ct);
            if (request.SessionId == Guid.Empty || request.Length < 0
                || request.Length > settings.GetServerSettings().MaxChunkSizeBytes
                || request.Hash.Length != Hasher.HashSizeInBytes * 2
                || request.Hash.Any(character => !char.IsAsciiHexDigit(character)))
            {
                throw new BadRequestException("Invalid upload chunk.");
            }
            byte[] hash = Hasher.FromHexStringHash(request.Hash);
            await ingest.UpsertChunkAsync(access.CreatedByUserId, request.Content, request.Length, hash, ct);
            sessions.Record(access.ShareId, request.NodeId, request.SessionId, Hasher.ToHexStringHash(hash));
            return true;
        }
    }
}
