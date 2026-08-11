// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Hubs;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quartz;
using FileVersionDto = Cotton.Files.FileVersionDto;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for file operations.
    /// </summary>
    [ApiController]
    public class FileController(
        IMediator _mediator,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges,
        ISchedulerFactory _scheduler,
        IHubContext<EventHub> _hubContext,
        FileManifestService _fileManifestService,
        FileVersionService _versions,
        UserStorageQuotaService _quota,
        FileGraphIntegrityVerifier _fileGraphIntegrity,
        ILayoutMutationGate _layoutGate,
        ILogger<FileController> _logger) : ControllerBase
    {

        /// <summary>
        /// Deletes file.
        /// </summary>
        [Authorize]
        [HttpDelete(Routes.V1.Files + "/{nodeFileId:guid}")]
        public async Task<IActionResult> DeleteFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            Guid? parentNodeId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => (Guid?)x.NodeId)
                .SingleOrDefaultAsync();
            DeleteFileQuery query = new(userId, nodeFileId, skipTrash, FileETags.ReadIfMatch(Request));
            await _mediator.Send(query);

            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                "FileDeleted",
                new NodeFileDeletedEventDto(nodeFileId, parentNodeId));
            return NoContent();
        }

        /// <summary>
        /// Restores file.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Files + "/{nodeFileId:guid}/restore")]
        public async Task<IActionResult> RestoreFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] RestoreItemRequestDto? request)
        {
            Guid userId = User.GetUserId();
            request ??= new RestoreItemRequestDto();

            RestoreOutcomeDto outcome = await _mediator.Send(new RestoreFileQuery(
                userId,
                nodeFileId,
                request.CreateMissingParents,
                request.Overwrite));

            if (outcome.Status == RestoreStatus.Restored)
            {
                object restoredFilePayload = outcome.RestoredFile is not null
                    ? outcome.RestoredFile
                    : new { id = nodeFileId };
                await _hubContext.Clients.User(userId.ToString()).SendAsync(
                    "FileRestored",
                    restoredFilePayload);
            }

            return Ok(outcome);
        }

        /// <summary>
        /// Moves file.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/move")]
        public async Task<IActionResult> MoveFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] MoveFileRequestDto request)
        {
            MoveFileCommand command = new()
            {
                NodeFileId = nodeFileId,
                ParentId = request.ParentId,
                UserId = User.GetUserId(),
                ExpectedETag = FileETags.ReadIfMatch(Request),
            };
            NodeFileManifestDto dto = await _mediator.Send(command);
            return Ok(dto);
        }

        /// <summary>
        /// Renames file.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/rename")]
        public async Task<IActionResult> RenameFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] RenameFileRequestDto request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            Guid? layoutId = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync();
            if (layoutId is null)
            {
                return CottonResult.NotFound("File not found.");
            }
            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(layoutId.Value, HttpContext.RequestAborted);
            await using IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync();

            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile is null || nodeFile.Node.Type != NodeType.Default)
            {
                return CottonResult.NotFound("File not found.");
            }

            if (!FileETags.MatchesIfMatchHeader(FileETags.ReadIfMatch(Request), nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>("File content changed before rename.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x =>
                    x.NodeId == nodeFile.NodeId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.Id != nodeFileId);
            if (fileExists)
            {
                return this.ApiConflict("A file with the same name key already exists in this folder: " + nameKey);
            }

            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == nodeFile.NodeId &&
                    x.OwnerId == userId &&
                    x.Type == nodeFile.Node.Type &&
                    x.NameKey == nameKey);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in this folder: " + nameKey);
            }

            nodeFile.SetName(request.Name);
            _syncChanges.StageFileChange(SyncChangeKind.FileRenamed, nodeFile, nodeFile.Node.LayoutId);
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();
            NodeFileManifestDto mapped = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileRenamed", mapped);
            return Ok(mapped);
        }

        /// <summary>
        /// Updates file metadata.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/metadata")]
        [ProducesResponseType<NodeFileManifestDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFileMetadata(
            [FromRoute] Guid nodeFileId,
            [FromBody] Dictionary<string, string?>? patch)
        {
            if (patch is null)
            {
                return CottonResult.BadRequest("Metadata patch is required.");
            }

            if (patch.Any(x => string.IsNullOrWhiteSpace(x.Key)))
            {
                return CottonResult.BadRequest("Metadata keys must be non-empty strings.");
            }

            if (patch.Any(x => x.Value is null))
            {
                return CottonResult.BadRequest("Metadata values must be strings.");
            }

            Guid userId = User.GetUserId();
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile is null || nodeFile.Node.Type != NodeType.Default)
            {
                return CottonResult.NotFound("File not found.");
            }

            Dictionary<string, string> metadata = nodeFile.Metadata is null
                ? []
                : new Dictionary<string, string>(nodeFile.Metadata);
            foreach ((string? key, string? value) in patch)
            {
                metadata[key] = value!;
            }

            nodeFile.Metadata = metadata;
            _syncChanges.StageFileChange(SyncChangeKind.FileContentUpdated, nodeFile, nodeFile.Node.LayoutId);
            await _dbContext.SaveChangesAsync();

            NodeFileManifestDto mapped = nodeFile.Adapt<NodeFileManifestDto>();
            try
            {
                await _hubContext.Clients.User(userId.ToString()).SendAsync("FileUpdated", mapped);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send file metadata update notification for file {NodeFileId}",
                    nodeFileId);
            }

            return Ok(mapped);
        }

        /// <summary>
        /// Ensures content metadata has been extracted for the file.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Files + "/{nodeFileId:guid}/metadata/extract")]
        [ProducesResponseType<NodeFileManifestDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<CottonResult>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ExtractFileMetadata([FromRoute] Guid nodeFileId)
        {
            NodeFileManifestDto? mapped = await _mediator.Send(new ExtractFileManifestMetadataRequest
            {
                NodeFileId = nodeFileId,
                UserId = User.GetUserId(),
                Notify = true,
            });

            return mapped is null
                ? CottonResult.NotFound("File not found.")
                : Ok(mapped);
        }

        /// <summary>
        /// Gets file versions.
        /// </summary>
        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/versions")]
        public async Task<IActionResult> GetFileVersions(
            [FromRoute] Guid nodeFileId,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            IReadOnlyList<FileVersionDto> versions = await _versions.ListVersionsAsync(userId, nodeFileId, cancellationToken);
            return Ok(versions);
        }

        /// <summary>
        /// Restores file version.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Files + "/{nodeFileId:guid}/versions/{versionId:guid}/restore")]
        public async Task<IActionResult> RestoreFileVersion(
            [FromRoute] Guid nodeFileId,
            [FromRoute] Guid versionId,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            NodeFileManifestDto restored = await _versions.RestoreVersionAsync(userId, nodeFileId, versionId, cancellationToken);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileUpdated", restored, cancellationToken);
            return Ok(restored);
        }

        /// <summary>
        /// Deletes file version.
        /// </summary>
        [Authorize]
        [HttpDelete(Routes.V1.Files + "/{nodeFileId:guid}/versions/{versionId:guid}")]
        public async Task<IActionResult> DeleteFileVersion(
            [FromRoute] Guid nodeFileId,
            [FromRoute] Guid versionId,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            await _versions.DeleteVersionAsync(userId, nodeFileId, versionId, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Downloads file version.
        /// </summary>
        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/versions/{versionId:guid}/download-link")]
        public async Task<IActionResult> DownloadFileVersion(
            [FromRoute] Guid nodeFileId,
            [FromRoute] Guid versionId,
            [FromQuery] int expireAfterMinutes = 1440,
            CancellationToken cancellationToken = default)
        {
            Guid userId = User.GetUserId();
            string link = await _versions.CreateVersionDownloadLinkAsync(
                userId,
                nodeFileId,
                versionId,
                expireAfterMinutes,
                cancellationToken);
            return Ok(link);
        }

        /// <summary>
        /// Downloads an owned file through normal bearer-token authentication.
        /// </summary>
        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/content")]
        public async Task<IActionResult> DownloadOwnedFileContent(
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool download = false)
        {
            Guid userId = User.GetUserId();
            NodeFile? nodeFile = await LoadOwnedDefaultNodeFileWithContentAsync(nodeFileId, userId);
            if (nodeFile is null)
            {
                return CottonResult.NotFound("Node file not found");
            }

            _fileGraphIntegrity.RequireValidContent(_dbContext, nodeFile, "file.content");
            EnsureContentETagPrecondition(nodeFile, "File content changed before download.");
            return FileDownloadResultFactory.Create(Response, _storage, nodeFile, download);
        }

        /// <summary>
        /// Gets an owned file content manifest with ordered verification chunks.
        /// </summary>
        [Authorize]
        [HttpGet(Routes.V1.Files + "/{nodeFileId:guid}/content-manifest")]
        public async Task<IActionResult> GetOwnedFileContentManifest([FromRoute] Guid nodeFileId)
        {
            Guid userId = User.GetUserId();
            NodeFile? nodeFile = await LoadOwnedDefaultNodeFileWithContentAsync(nodeFileId, userId);
            if (nodeFile is null)
            {
                return CottonResult.NotFound("Node file not found");
            }

            _fileGraphIntegrity.RequireValidContent(_dbContext, nodeFile, "file.content-manifest");
            EnsureContentETagPrecondition(nodeFile, "File content changed before manifest fetch.");
            return Ok(CreateContentManifestDto(nodeFile));
        }

        private Task<NodeFile?> LoadOwnedDefaultNodeFileWithContentAsync(Guid nodeFileId, Guid userId)
        {
            return _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x =>
                    x.Id == nodeFileId &&
                    x.OwnerId == userId &&
                    x.Node.Type == NodeType.Default);
        }

        private void EnsureContentETagPrecondition(NodeFile nodeFile, string message)
        {
            if (!FileETags.MatchesIfMatchHeader(FileETags.ReadIfMatch(Request), nodeFile))
            {
                throw new FilePreconditionFailedException<NodeFile>(message);
            }
        }

        /// <summary>
        /// Updates file content.
        /// </summary>
        [Authorize]
        [HttpPatch(Routes.V1.Files + "/{nodeFileId:guid}/update-content")]
        public async Task<IActionResult> UpdateFileContent(
            [FromRoute] Guid nodeFileId,
            [FromBody] CreateFileFromChunksRequestDto request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            Guid? layoutId = await GetOwnedFileLayoutIdAsync(nodeFileId, userId);
            if (layoutId is null)
            {
                return this.ApiNotFound("Node file not found.");
            }

            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            FileManifest newFile = await ResolveUpdateManifestAsync(request, proposedHash, userId);

            await using IAsyncDisposable layoutGate = await _layoutGate.EnterAsync(layoutId.Value, HttpContext.RequestAborted);
            NodeFile nodeFile;
            await using (IAsyncDisposable quotaGate = await _quota.EnterMutationAsync(userId, HttpContext.RequestAborted))
            await using (IDbContextTransaction tx = await _dbContext.Database.BeginTransactionAsync())
            {
                NodeFile? editableNodeFile = await LoadEditableNodeFileAsync(nodeFileId, userId);
                if (editableNodeFile is null)
                {
                    return this.ApiNotFound("Node file not found.");
                }

                nodeFile = editableNodeFile;
                if (!FileETags.MatchesIfMatchHeader(FileETags.ReadIfMatch(Request), nodeFile))
                {
                    throw new FilePreconditionFailedException<NodeFile>("File content changed before update.");
                }

                string nameKey = NameValidator.NormalizeAndGetNameKey(normalizedName);
                string? conflictMessage = await FindUpdateNameConflictAsync(nodeFile, userId, nodeFileId, nameKey);
                if (conflictMessage is not null)
                {
                    return this.ApiConflict(conflictMessage);
                }

                long addedBytes = await _quota.EnsureCanChangeFileManifestAsync(userId, nodeFile.Id, newFile.Id);
                FileVersionCaptureResult capture = await ApplyUpdatedFileContentAsync(
                    nodeFile,
                    newFile,
                    proposedHash,
                    normalizedName,
                    request.Metadata,
                    userId);

                _syncChanges.StageFileChange(SyncChangeKind.FileContentUpdated, nodeFile, layoutId.Value);
                await _dbContext.SaveChangesAsync();
                await tx.CommitAsync();
                _quota.RecordLogicalBytesAdded(userId, addedBytes);
                if (capture.RemovedBytes > 0)
                {
                    _quota.RecordLogicalBytesRemoved(userId, capture.RemovedBytes);
                }
            }

            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            await _scheduler.TriggerJobAsync<ExtractFileMetadataJob>();

            NodeFileManifestDto mapped = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileUpdated", mapped);
            return Ok(mapped);
        }

        private async Task<Guid?> GetOwnedFileLayoutIdAsync(Guid nodeFileId, Guid userId)
        {
            return await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .Select(x => (Guid?)x.Node.LayoutId)
                .SingleOrDefaultAsync();
        }

        private async Task<FileManifest> ResolveUpdateManifestAsync(
            CreateFileFromChunksRequestDto request,
            byte[] proposedHash,
            Guid userId)
        {
            List<Chunk> chunks = await _fileManifestService.GetChunksAsync([.. request.ChunkHashes], userId);
            return await _fileManifestService.GetReusableOwnedManifestAsync(proposedHash, userId)
                ?? await _fileManifestService.CreateNewFileManifestAsync(
                    chunks,
                    request.Name,
                    request.ContentType,
                    proposedHash,
                    userId);
        }

        private async Task<NodeFile?> LoadEditableNodeFileAsync(Guid nodeFileId, Guid userId)
        {
            return await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId && x.Node.Type == NodeType.Default)
                .SingleOrDefaultAsync();
        }

        private async Task<string?> FindUpdateNameConflictAsync(
            NodeFile nodeFile,
            Guid userId,
            Guid nodeFileId,
            string nameKey)
        {
            if (string.Equals(nodeFile.NameKey, nameKey, StringComparison.Ordinal))
            {
                return null;
            }

            bool fileExists = await _dbContext.NodeFiles.AnyAsync(x =>
                x.NodeId == nodeFile.NodeId &&
                x.OwnerId == userId &&
                x.NameKey == nameKey &&
                x.Id != nodeFileId);
            if (fileExists)
            {
                return "A file with the same name key already exists in this folder: " + nameKey;
            }

            bool nodeExists = await _dbContext.Nodes.AnyAsync(x =>
                x.ParentId == nodeFile.NodeId &&
                x.OwnerId == userId &&
                x.Type == nodeFile.Node.Type &&
                x.NameKey == nameKey);
            return nodeExists
                ? "A folder with the same name key already exists in this folder: " + nameKey
                : null;
        }

        private async Task<FileVersionCaptureResult> ApplyUpdatedFileContentAsync(
            NodeFile nodeFile,
            FileManifest newFile,
            byte[] proposedHash,
            string normalizedName,
            Dictionary<string, string>? metadata,
            Guid userId)
        {
            FileVersionCaptureResult capture = FileVersionCaptureResult.Empty;
            if (!nodeFile.FileManifest.ProposedContentHash.SequenceEqual(proposedHash))
            {
                capture = await _versions.CaptureAndUpdateManifestAsync(nodeFile, newFile.Id, userId);
                nodeFile.FileManifest = newFile;
            }

            nodeFile.SetName(normalizedName);
            if (metadata is not null)
            {
                nodeFile.Metadata = metadata.Count > 0
                    ? new Dictionary<string, string>(metadata)
                    : [];
            }

            return capture;
        }

        private static FileContentManifestDto CreateContentManifestDto(NodeFile nodeFile)
        {
            FileManifest manifest = nodeFile.FileManifest;
            List<FileManifestChunk> orderedChunks = [.. manifest.FileManifestChunks.OrderBy(x => x.ChunkOrder)];
            var chunkDtos = new List<FileContentManifestChunkDto>(orderedChunks.Count);
            long offset = 0;

            foreach (FileManifestChunk manifestChunk in orderedChunks)
            {
                string chunkHash = Hasher.ToHexStringHash(manifestChunk.ChunkHash);
                long length = manifestChunk.Chunk.PlainSizeBytes;
                chunkDtos.Add(new FileContentManifestChunkDto
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
                ChunkSizeBytes = ResolveNominalChunkSizeBytes(chunkDtos),
                Chunks = chunkDtos,
            };
        }

        private static long? ResolveNominalChunkSizeBytes(IReadOnlyList<FileContentManifestChunkDto> chunks)
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

        /// <summary>
        /// Creates file from chunks.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Files + "/from-chunks")]
        public async Task<IActionResult> CreateFileFromChunks([FromBody] CreateFileFromChunksRequestDto request)
        {
            Guid userId = User.GetUserId();
            NodeFileManifestDto manifest = await _mediator.Send(ToCreateFileRequest(request, userId));
            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            await _scheduler.TriggerJobAsync<ExtractFileMetadataJob>();
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileCreated", manifest);
            return Ok(manifest);
        }

        private static CreateFileRequest ToCreateFileRequest(CreateFileFromChunksRequestDto request, Guid userId)
        {
            return new CreateFileRequest
            {
                NodeId = request.NodeId,
                ChunkHashes = [.. request.ChunkHashes],
                Name = request.Name,
                ContentType = request.ContentType,
                Hash = request.Hash,
                OriginalNodeFileId = request.OriginalNodeFileId,
                Metadata = request.Metadata,
                Validate = request.Validate,
                UserId = userId,
            };
        }
    }
}
