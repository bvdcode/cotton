// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public class CreateFileRequest : IRequest<NodeFileManifestDto>
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
        SettingsProvider _settingsProvider,
        FileManifestService _fileManifestService) : IRequestHandler<CreateFileRequest, NodeFileManifestDto>
    {
        public async Task<NodeFileManifestDto> Handle(CreateFileRequest request, CancellationToken cancellationToken)
        {
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);

            // Resolve once before expensive manifest/hash work so invalid targets fail fast.
            // The target is re-read inside the layout lock before the namespace write.
            var preLockNode = await GetTargetNodeAsync(request, layout.Id, tracking: false, cancellationToken);
            string nameKey = ValidateNameAndGetKey(request.Name);

            List<Chunk> chunks = await _fileManifestService.GetChunksAsync(request.ChunkHashes, request.UserId, cancellationToken);
            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            var fileManifest = await GetOrCreateFileManifestAsync(chunks, request, proposedHash, cancellationToken);

            await ValidateContentHashIfRequestedAsync(request, fileManifest, proposedHash, cancellationToken);

            // Cross-table namespace serialization: cf. LayoutLocks.
            // Keep the lock scoped to the actual namespace mutation; chunk lookup,
            // manifest dedup and optional hash validation can be slow but do not
            // depend on per-parent NameKey state.
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await LayoutLocks.AcquireForLayoutAsync(_dbContext, preLockNode.LayoutId, cancellationToken);

            var node = await GetTargetNodeAsync(request, preLockNode.LayoutId, tracking: true, cancellationToken);
            await EnsureNoDuplicatesAsync(node.Id, request.UserId, nameKey, cancellationToken);

            var nodeFile = await CreateNodeFileAsync(node, fileManifest, request, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return MapToDto(nodeFile, fileManifest);
        }

        private async Task<Node> GetTargetNodeAsync(CreateFileRequest request, Guid layoutId, bool tracking, CancellationToken ct)
        {
            var query = _dbContext.Nodes
                .Where(x => x.Id == request.NodeId
                    && x.Type == NodeType.Default
                    && x.OwnerId == request.UserId
                    && x.LayoutId == layoutId);

            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            var node = await query.SingleOrDefaultAsync(cancellationToken: ct);

            return node ?? throw new EntryPointNotFoundException("Layout node not found.");
        }

        private static string ValidateNameAndGetKey(string name)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(name, out _, out string errorMessage);
            if (!isValidName)
            {
                throw new BadRequestException($"Invalid file name: {errorMessage}");
            }

            return NameValidator.NormalizeAndGetNameKey(name);
        }

        private async Task EnsureNoDuplicatesAsync(Guid nodeId, Guid userId, string nameKey, CancellationToken ct)
        {
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x => x.NodeId == nodeId
                    && x.OwnerId == userId
                    && x.NameKey == nameKey,
                    cancellationToken: ct);
            if (fileExists)
            {
                throw new DuplicateException(nameKey);
            }

            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x => x.ParentId == nodeId
                    && x.OwnerId == userId
                    && x.NameKey == nameKey
                    && x.Type == NodeType.Default,
                    ct);
            if (nodeExists)
            {
                throw new DuplicateException(nameKey);
            }
        }

        private async Task<FileManifest> GetOrCreateFileManifestAsync(
            List<Chunk> chunks,
            CreateFileRequest request,
            byte[] proposedHash,
            CancellationToken ct)
        {
            var fileManifest = await _dbContext.FileManifests
                .FirstOrDefaultAsync(x => x.ComputedContentHash == proposedHash || x.ProposedContentHash == proposedHash, ct);
            if (fileManifest is not null)
            {
                await _fileManifestService.ClearGcSchedulesForManifestReferencesAsync(fileManifest.Id, ct);

                var settings = _settingsProvider.GetServerSettings();
                if (!settings.AllowCrossUserDeduplication
                    && (fileManifest.SmallFilePreviewHashEncrypted is not null || fileManifest.PreviewGenerationError is not null))
                {
                    fileManifest.SmallFilePreviewHashEncrypted = null;
                    fileManifest.PreviewGenerationError = null;
                    await _dbContext.SaveChangesAsync(ct);
                }
                return fileManifest;
            }

            return await _fileManifestService.CreateNewFileManifestAsync(chunks, request.Name, request.ContentType, proposedHash, ct);
        }

        private async Task ValidateContentHashIfRequestedAsync(
            CreateFileRequest request,
            FileManifest fileManifest,
            byte[] proposedHash,
            CancellationToken ct)
        {
            if (!request.Validate || fileManifest.ComputedContentHash != null)
            {
                return;
            }

            string[] hashes = fileManifest.FileManifestChunks.GetChunkHashes();
            PipelineContext pipelineContext = new()
            {
                FileSizeBytes = fileManifest.SizeBytes
            };

            using Stream stream = _storage.GetBlobStream(hashes, pipelineContext);
            var computedContentHash = Hasher.HashData(stream);
            if (!computedContentHash.SequenceEqual(proposedHash))
            {
                _logger.LogWarning(
                    "File content hash mismatch for user {UserId}, file {FileName}.",
                    request.UserId,
                    request.Name);
                throw new BadRequestException("File content hash does not match the provided hash.");
            }

            fileManifest.ComputedContentHash = computedContentHash;
            await _dbContext.SaveChangesAsync(ct);
        }

        private async Task<NodeFile> CreateNodeFileAsync(
            Node node,
            FileManifest fileManifest,
            CreateFileRequest request,
            CancellationToken ct)
        {
            NodeFile newNodeFile = new()
            {
                Node = node,
                OwnerId = request.UserId,
                FileManifest = fileManifest,
            };
            newNodeFile.SetName(request.Name);

            await _dbContext.NodeFiles.AddAsync(newNodeFile, ct);
            if (!request.OriginalNodeFileId.HasValue)
            {
                await _dbContext.SaveChangesAsync(ct);
                newNodeFile.OriginalNodeFileId = newNodeFile.Id;
            }
            else
            {
                newNodeFile.OriginalNodeFileId = request.OriginalNodeFileId.Value;
            }

            await _dbContext.SaveChangesAsync(ct);
            return newNodeFile;
        }

        private static NodeFileManifestDto MapToDto(NodeFile nodeFile, FileManifest fileManifest)
        {
            return new NodeFileManifestDto()
            {
                ContentType = fileManifest.ContentType,
                CreatedAt = fileManifest.CreatedAt,
                Name = nodeFile.Name,
                OwnerId = nodeFile.OwnerId,
                SizeBytes = fileManifest.SizeBytes,
                UpdatedAt = fileManifest.UpdatedAt,
                Id = fileManifest.Id,
            };
        }
    }
}
