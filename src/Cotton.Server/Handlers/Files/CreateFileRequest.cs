// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public class CreateFileRequest : IRequest<FileManifestDto>
    {
        public Guid NodeId { get; set; }
        public string[] ChunkHashes { get; set; } = [];
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string Hash { get; set; } = null!;
        public Guid? OriginalNodeFileId { get; set; }
        public bool Validate { get; set; }
        public Guid UserId { get; set; }
    }

    public class CreateFileRequestHandler(
        CottonDbContext _dbContext,
        IStoragePipeline _storage,
        ILogger<CreateFileRequestHandler> _logger,
        ILayoutService _layouts,
        FileManifestService _fileManifestService) : IRequestHandler<CreateFileRequest, FileManifestDto>
    {
        public async Task<FileManifestDto> Handle(CreateFileRequest request, CancellationToken cancellationToken)
        {
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);
            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.Type == NodeType.Default && x.OwnerId == request.UserId && x.LayoutId == layout.Id)
                .SingleOrDefaultAsync(cancellationToken: cancellationToken)
                    ?? throw new EntryPointNotFoundException("Layout node not found.");

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
            // Check for duplicate files in the target folder
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x => x.NodeId == node.Id
                    && x.OwnerId == request.UserId
                    && x.NameKey == nameKey,
                    cancellationToken: cancellationToken);
            if (fileExists)
            {
                throw new DuplicateException(nameKey);
            }

            // Check for duplicate nodes (subfolders) in the target folder
            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == node.Id &&
                    x.OwnerId == request.UserId &&
                    x.NameKey == nameKey &&
                    x.Type == NodeType.Default, cancellationToken: cancellationToken);
            if (nodeExists)
            {
                throw new DuplicateException(nameKey);
            }

            List<Chunk> chunks = await _fileManifestService.GetChunksAsync(request.ChunkHashes, request.UserId, cancellationToken);

            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name, out string normalizedName, out string errorMessage);
            if (!isValidName)
            {
                throw new BadRequestException($"Invalid file name: {errorMessage}");
            }

            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            var newFile = await _dbContext.FileManifests
                .FirstOrDefaultAsync(x => x.ComputedContentHash == proposedHash || x.ProposedContentHash == proposedHash, cancellationToken: cancellationToken)
                ?? await _fileManifestService.CreateNewFileManifestAsync(chunks, request.Name, request.ContentType, proposedHash, cancellationToken);

            NodeFile newNodeFile = new()
            {
                Node = node,
                OwnerId = request.UserId,
                FileManifest = newFile,
            };
            newNodeFile.SetName(request.Name);
            if (request.Validate && newFile.ComputedContentHash == null)
            {
                string[] hashes = newFile.FileManifestChunks.GetChunkHashes();
                PipelineContext pipelineContext = new()
                {
                    FileSizeBytes = newFile.SizeBytes
                };
                using Stream stream = _storage.GetBlobStream(hashes, pipelineContext);
                var computedContentHash = Hasher.HashData(stream);
                if (!computedContentHash.SequenceEqual(proposedHash))
                {
                    _logger.LogWarning("File content hash mismatch for user {UserId}, file {FileName}. Expected {ExpectedHash}, computed {ComputedHash}.",
                        request.UserId,
                        request.Name,
                        request.Hash,
                        Hasher.ToHexStringHash(computedContentHash));
                    throw new BadRequestException("File content hash does not match the provided hash.");
                }
                newFile.ComputedContentHash = computedContentHash;
            }
            await _dbContext.NodeFiles.AddAsync(newNodeFile, cancellationToken);
            if (!request.OriginalNodeFileId.HasValue)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                newNodeFile.OriginalNodeFileId = newNodeFile.Id;
            }
            else
            {
                newNodeFile.OriginalNodeFileId = request.OriginalNodeFileId.Value;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new FileManifestDto()
            {
                ContentType = newFile.ContentType,
                CreatedAt = newFile.CreatedAt,
                Name = newNodeFile.Name,
                OwnerId = newNodeFile.OwnerId,
                SizeBytes = newFile.SizeBytes,
                UpdatedAt = newFile.UpdatedAt,
                Id = newFile.Id,
            };
        }
    }
}
