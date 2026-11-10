// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Layouts
{
    public record CreateSharedFileRequest(string Token, Guid SessionId, CreateFileFromChunksRequestDto File)
        : IRequest<NodeFileManifestDto>;

    public class CreateSharedFileRequestHandler(IMediator mediator, SharedUploadSessionCache sessions)
        : IRequestHandler<CreateSharedFileRequest, NodeFileManifestDto>
    {
        public async Task<NodeFileManifestDto> Handle(CreateSharedFileRequest request, CancellationToken ct)
        {
            SharedNodeAccess access = await mediator.Send(
                new RequireSharedUploadAccessQuery(request.Token, request.File.NodeId), ct);
            if (request.SessionId == Guid.Empty || request.File.Hash.Length != Hasher.HashSizeInBytes * 2
                || request.File.Hash.Any(character => !char.IsAsciiHexDigit(character))
                || (request.File.ChunkHashes.Count > 0
                    && !sessions.ContainsAll(access.ShareId, request.File.NodeId, request.SessionId, request.File.ChunkHashes)))
            {
                throw new BadRequestException("Upload session expired or file content has not been uploaded in this session.");
            }
            return await mediator.Send(new CreateFileRequest
            {
                UserId = access.CreatedByUserId,
                NodeId = request.File.NodeId,
                Name = request.File.Name,
                ContentType = request.File.ContentType,
                Hash = request.File.Hash,
                ChunkHashes = [.. request.File.ChunkHashes],
                ForceDisableDeduplication = true,
                PublicShareToken = request.Token,
            }, ct);
        }
    }
}
