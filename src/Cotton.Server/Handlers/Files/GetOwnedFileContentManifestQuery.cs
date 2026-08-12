// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Files;
using Cotton.Server.Models;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Files
{
    public record GetOwnedFileContentManifestQuery(
        Guid UserId,
        Guid NodeFileId,
        string? ExpectedETag) : IRequest<FileContentManifestDto?>;

    public class GetOwnedFileContentManifestQueryHandler(IMediator _mediator)
        : IRequestHandler<GetOwnedFileContentManifestQuery, FileContentManifestDto?>
    {
        public async Task<FileContentManifestDto?> Handle(
            GetOwnedFileContentManifestQuery request,
            CancellationToken ct)
        {
            NodeFile? nodeFile = await _mediator.Send(
                new ResolveOwnedFileContentQuery(
                    request.UserId,
                    request.NodeFileId,
                    OwnedFileContentPurpose.Manifest,
                    request.ExpectedETag),
                ct);
            return nodeFile is null ? null : CreateContentManifest(nodeFile);
        }

        private static FileContentManifestDto CreateContentManifest(
            NodeFile nodeFile)
        {
            FileManifest manifest = nodeFile.FileManifest;
            List<FileManifestChunk> orderedChunks =
                [.. manifest.FileManifestChunks.OrderBy(x => x.ChunkOrder)];
            List<FileContentManifestChunkDto> chunks = new(orderedChunks.Count);
            long offset = 0;

            foreach (FileManifestChunk manifestChunk in orderedChunks)
            {
                string chunkHash = Hasher.ToHexStringHash(manifestChunk.ChunkHash);
                long length = manifestChunk.Chunk.PlainSizeBytes;
                chunks.Add(new FileContentManifestChunkDto
                {
                    Index = manifestChunk.ChunkOrder,
                    Offset = offset,
                    Length = length,
                    Hash = chunkHash,
                    ChunkId = chunkHash,
                });
                offset = checked(offset + length);
            }

            return new FileContentManifestDto
            {
                NodeFileId = nodeFile.Id,
                FileManifestId = manifest.Id,
                ContentHash = Hasher.ToHexStringHash(manifest.ProposedContentHash),
                ETag = FileETags.GetContentETag(manifest),
                SizeBytes = manifest.SizeBytes,
                ChunkSizeBytes = ResolveNominalChunkSizeBytes(chunks),
                Chunks = chunks,
            };
        }

        private static long? ResolveNominalChunkSizeBytes(
            IReadOnlyList<FileContentManifestChunkDto> chunks)
        {
            if (chunks.Count == 0)
            {
                return 0;
            }

            if (chunks.Count == 1)
            {
                return chunks[0].Length;
            }

            long firstChunkLength = chunks[0].Length;
            for (int i = 0; i < chunks.Count - 1; i++)
            {
                if (chunks[i].Length != firstChunkLength)
                {
                    return null;
                }
            }

            return firstChunkLength;
        }
    }
}
