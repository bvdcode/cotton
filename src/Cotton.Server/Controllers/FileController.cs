// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for file operations.
    /// </summary>
    [ApiController]
    public class FileController(
        IMediator _mediator,
        IStoragePipeline _storage) : ControllerBase
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
            DeleteFileRequest request = new(
                userId,
                nodeFileId,
                skipTrash,
                FileETags.ReadIfMatch(Request));
            await _mediator.Send(
                request,
                HttpContext.RequestAborted);
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

            return Ok(result.File!);
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

            return Ok(result.File!);
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
            IReadOnlyList<FileVersionDto> versions = await _mediator.Send(
                new GetFileVersionsQuery(userId, nodeFileId),
                cancellationToken);
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
            NodeFileManifestDto restored = await _mediator.Send(
                new RestoreFileVersionRequest(userId, nodeFileId, versionId),
                cancellationToken);
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
            await _mediator.Send(
                new DeleteFileVersionRequest(userId, nodeFileId, versionId),
                cancellationToken);
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
            string link = await _mediator.Send(
                new CreateFileVersionDownloadLinkRequest(
                    userId,
                    nodeFileId,
                    versionId,
                    expireAfterMinutes),
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

            return Ok(result.File!);
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
