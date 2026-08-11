// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Updates metadata attached to an owned file.
    /// </summary>
    public record UpdateFileMetadataRequest(
        Guid UserId,
        Guid NodeFileId,
        Dictionary<string, string?>? Patch) : IRequest<UpdateFileMetadataResult>;

    /// <summary>
    /// Handles file metadata updates.
    /// </summary>
    public class UpdateFileMetadataRequestHandler(
        CottonDbContext _dbContext,
        ISyncChangeRecorder _syncChanges)
        : IRequestHandler<UpdateFileMetadataRequest, UpdateFileMetadataResult>
    {
        /// <summary>
        /// Validates and applies the metadata patch.
        /// </summary>
        public async Task<UpdateFileMetadataResult> Handle(
            UpdateFileMetadataRequest request,
            CancellationToken ct)
        {
            string? validationError = ValidatePatch(request.Patch);
            if (validationError is not null)
            {
                return Failure(
                    UpdateFileMetadataStatus.InvalidPatch,
                    validationError);
            }

            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == request.NodeFileId
                    && x.OwnerId == request.UserId)
                .SingleOrDefaultAsync(ct);
            if (nodeFile is null || nodeFile.Node.Type != NodeType.Default)
            {
                return Failure(
                    UpdateFileMetadataStatus.FileNotFound,
                    "File not found.");
            }

            Dictionary<string, string> metadata = nodeFile.Metadata is null
                ? []
                : new Dictionary<string, string>(nodeFile.Metadata);
            foreach ((string key, string? value) in request.Patch!)
            {
                metadata[key] = value!;
            }

            nodeFile.Metadata = metadata;
            _syncChanges.StageFileChange(
                SyncChangeKind.FileContentUpdated,
                nodeFile,
                nodeFile.Node.LayoutId);
            await _dbContext.SaveChangesAsync(ct);

            return new UpdateFileMetadataResult(
                UpdateFileMetadataStatus.Updated,
                nodeFile.Adapt<NodeFileManifestDto>());
        }

        private static string? ValidatePatch(
            IReadOnlyDictionary<string, string?>? patch)
        {
            if (patch is null)
            {
                return "Metadata patch is required.";
            }

            if (patch.Keys.Any(string.IsNullOrWhiteSpace))
            {
                return "Metadata keys must be non-empty strings.";
            }

            return patch.Values.Any(value => value is null)
                ? "Metadata values must be strings."
                : null;
        }

        private static UpdateFileMetadataResult Failure(
            UpdateFileMetadataStatus status,
            string error) => new(status, Error: error);
    }
}
