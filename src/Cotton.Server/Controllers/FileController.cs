// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Hubs;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Quartz.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Quartz;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for file operations.
    /// </summary>
    [ApiController]
    public class FileController(
        IMediator _mediator,
        IStoragePipeline _storage,
        ISchedulerFactory _scheduler,
        IHubContext<EventHub> _hubContext,
        FileVersionService _versions,
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
            DeleteFileQuery query = new(userId, nodeFileId, skipTrash, FileETags.ReadIfMatch(Request));
            Guid? parentNodeId = await _mediator.Send(
                query,
                HttpContext.RequestAborted);

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
            Guid userId = User.GetUserId();
            RenameFileResult result = await _mediator.Send(
                new RenameFileRequest(
                    userId,
                    nodeFileId,
                    request.Name,
                    FileETags.ReadIfMatch(Request)),
                HttpContext.RequestAborted);
            if (result.Status != RenameFileStatus.Renamed)
            {
                return result.Status switch
                {
                    RenameFileStatus.InvalidName => CottonResult.BadRequest(result.Error!),
                    RenameFileStatus.FileNotFound => CottonResult.NotFound(result.Error!),
                    RenameFileStatus.NameConflict => this.ApiConflict(result.Error!),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
                };
            }

            NodeFileManifestDto mapped = result.File!;
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
            Guid userId = User.GetUserId();
            UpdateFileMetadataResult result = await _mediator.Send(
                new UpdateFileMetadataRequest(userId, nodeFileId, patch),
                HttpContext.RequestAborted);
            if (result.Status != UpdateFileMetadataStatus.Updated)
            {
                return result.Status switch
                {
                    UpdateFileMetadataStatus.InvalidPatch => CottonResult.BadRequest(result.Error!),
                    UpdateFileMetadataStatus.FileNotFound => CottonResult.NotFound(result.Error!),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
                };
            }

            NodeFileManifestDto mapped = result.File!;
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
            NodeFile? nodeFile = await _mediator.Send(
                new ResolveOwnedFileContentQuery(
                    userId,
                    nodeFileId,
                    OwnedFileContentPurpose.Download,
                    FileETags.ReadIfMatch(Request)),
                HttpContext.RequestAborted);
            if (nodeFile is null)
            {
                return CottonResult.NotFound("Node file not found");
            }

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
            FileContentManifestDto? manifest = await _mediator.Send(
                new GetOwnedFileContentManifestQuery(
                    userId,
                    nodeFileId,
                    FileETags.ReadIfMatch(Request)),
                HttpContext.RequestAborted);
            if (manifest is null)
            {
                return CottonResult.NotFound("Node file not found");
            }

            return Ok(manifest);
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
            Guid userId = User.GetUserId();
            UpdateFileContentRequest command = new(
                userId,
                nodeFileId,
                request.Name,
                request.ContentType,
                request.Hash,
                [.. request.ChunkHashes],
                request.Metadata,
                FileETags.ReadIfMatch(Request));
            UpdateFileContentResult result = await _mediator.Send(
                command,
                HttpContext.RequestAborted);
            if (result.Status != UpdateFileContentStatus.Updated)
            {
                return result.Status switch
                {
                    UpdateFileContentStatus.InvalidName => CottonResult.BadRequest(result.Error!),
                    UpdateFileContentStatus.FileNotFound => this.ApiNotFound(result.Error!),
                    UpdateFileContentStatus.NameConflict => this.ApiConflict(result.Error!),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status)),
                };
            }

            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            await _scheduler.TriggerJobAsync<ExtractFileMetadataJob>();

            NodeFileManifestDto mapped = result.File!;
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileUpdated", mapped);
            return Ok(mapped);
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
