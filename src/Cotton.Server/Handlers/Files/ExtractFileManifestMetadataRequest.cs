// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Server.Services.FileMetadata;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Extracts deterministic content metadata for a file manifest.
    /// </summary>
    public class ExtractFileManifestMetadataRequest : IRequest<NodeFileManifestDto?>
    {
        /// <summary>
        /// Gets or sets the visible file entry identifier requested by a client.
        /// </summary>
        public Guid? NodeFileId { get; set; }

        /// <summary>
        /// Gets or sets the immutable file manifest identifier requested by maintenance jobs.
        /// </summary>
        public Guid? FileManifestId { get; set; }

        /// <summary>
        /// Gets or sets the user boundary for client-triggered extraction.
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether updated file DTOs should be pushed to connected clients.
        /// </summary>
        public bool Notify { get; set; }
    }

    /// <summary>
    /// Handles deterministic content metadata extraction.
    /// </summary>
    public class ExtractFileManifestMetadataRequestHandler(
        CottonDbContext _dbContext,
        IStoragePipeline _storage,
        FileContentMetadataExtractorProvider _extractorProvider,
        IEventNotificationService _eventNotification,
        ILogger<ExtractFileManifestMetadataRequestHandler> _logger)
        : IRequestHandler<ExtractFileManifestMetadataRequest, NodeFileManifestDto?>
    {
        private const string ExtractionFailedMessage = "File metadata extraction failed.";

        /// <inheritdoc />
        public async Task<NodeFileManifestDto?> Handle(
            ExtractFileManifestMetadataRequest request,
            CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            FileManifest? manifest = await LoadManifestAsync(request, cancellationToken);
            if (manifest is null)
            {
                return null;
            }

            if (IsCurrent(manifest))
            {
                return MapRequestedFile(request, manifest);
            }

            bool visibleMetadataChanged = await ExtractAndStoreAsync(manifest, cancellationToken);
            if (visibleMetadataChanged && request.Notify)
            {
                await NotifyManifestFilesUpdatedAsync(manifest, cancellationToken);
            }

            return MapRequestedFile(request, manifest);
        }

        private static void ValidateRequest(ExtractFileManifestMetadataRequest request)
        {
            if (request.NodeFileId is null && request.FileManifestId is null)
            {
                throw new ArgumentException("Either node file id or file manifest id is required.");
            }
        }

        private async Task<FileManifest?> LoadManifestAsync(
            ExtractFileManifestMetadataRequest request,
            CancellationToken cancellationToken)
        {
            IQueryable<FileManifest> query = _dbContext.FileManifests
                .Include(x => x.NodeFiles)
                .Include(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .AsSplitQuery();

            if (request.NodeFileId is Guid nodeFileId)
            {
                query = query.Where(x => x.NodeFiles.Any(nodeFile =>
                    nodeFile.Id == nodeFileId
                    && (!request.UserId.HasValue || nodeFile.OwnerId == request.UserId.Value)));
            }
            else if (request.FileManifestId is Guid fileManifestId)
            {
                query = query.Where(x => x.Id == fileManifestId);
                if (request.UserId is Guid userId)
                {
                    query = query.Where(x => x.NodeFiles.Any(nodeFile => nodeFile.OwnerId == userId));
                }
            }

            return await query.SingleOrDefaultAsync(cancellationToken);
        }

        private static bool IsCurrent(FileManifest manifest)
        {
            return manifest.MetadataExtractorVersion == FileContentMetadataExtractorProvider.CurrentVersion;
        }

        private async Task<bool> ExtractAndStoreAsync(FileManifest manifest, CancellationToken cancellationToken)
        {
            IFileContentMetadataExtractor? extractor = _extractorProvider.GetExtractor(manifest.ContentType);
            Dictionary<string, string>? oldMetadata = manifest.Metadata is null
                ? null
                : new Dictionary<string, string>(manifest.Metadata, StringComparer.Ordinal);

            if (extractor is null)
            {
                MarkExtractionFailure(manifest, "No metadata extractor matched the manifest content type.");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }

            try
            {
                IReadOnlyDictionary<string, string> extracted = await ExtractManifestMetadataAsync(
                    manifest,
                    extractor,
                    cancellationToken);

                manifest.Metadata = FileContentMetadataDictionary.ReplaceManagedValues(manifest.Metadata, extracted);
                manifest.MetadataExtractorVersion = FileContentMetadataExtractorProvider.CurrentVersion;
                manifest.MetadataExtractionError = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return !AreEquivalent(oldMetadata, manifest.Metadata);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogInformation(
                    ex,
                    "Rejected stale metadata update for file manifest {FileManifestId}",
                    manifest.Id);
                throw new WebApiException(
                    HttpStatusCode.Conflict,
                    nameof(FileManifest),
                    "The file manifest changed while metadata was being extracted. Retry the operation.");
            }
            catch (FileMetadataUnavailableException ex)
            {
                _logger.LogDebug(
                    "Metadata is unavailable for file manifest {FileManifestId}: {Reason}",
                    manifest.Id,
                    ex.Message);
                MarkExtractionFailure(manifest, ExtractionFailedMessage);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract metadata for file manifest {FileManifestId}", manifest.Id);
                MarkExtractionFailure(manifest, ExtractionFailedMessage);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        private async Task<IReadOnlyDictionary<string, string>> ExtractManifestMetadataAsync(
            FileManifest manifest,
            IFileContentMetadataExtractor extractor,
            CancellationToken cancellationToken)
        {
            string[] uids = manifest.FileManifestChunks.GetChunkHashes();
            PipelineContext pipelineContext = new()
            {
                FileSizeBytes = manifest.SizeBytes,
                ChunkLengths = manifest.FileManifestChunks.GetChunkLengths(),
            };

            await using Stream stream = _storage.GetBlobStream(uids, pipelineContext);
            return await extractor.ExtractAsync(stream, manifest.ContentType, cancellationToken);
        }

        private static void MarkExtractionFailure(FileManifest manifest, string message)
        {
            manifest.MetadataExtractorVersion = FileContentMetadataExtractorProvider.CurrentVersion;
            manifest.MetadataExtractionError = message;
        }

        private async Task NotifyManifestFilesUpdatedAsync(FileManifest manifest, CancellationToken cancellationToken)
        {
            foreach (NodeFile nodeFile in manifest.NodeFiles)
            {
                await _eventNotification.NotifyFileUpdatedAsync(nodeFile.Id, cancellationToken);
            }
        }

        private static NodeFileManifestDto? MapRequestedFile(
            ExtractFileManifestMetadataRequest request,
            FileManifest manifest)
        {
            NodeFile? nodeFile = request.NodeFileId is Guid nodeFileId
                ? manifest.NodeFiles.SingleOrDefault(x => x.Id == nodeFileId)
                : null;

            return nodeFile?.Adapt<NodeFileManifestDto>();
        }

        private static bool AreEquivalent(
            IReadOnlyDictionary<string, string>? left,
            IReadOnlyDictionary<string, string>? right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0))
            {
                return false;
            }

            if (left is null || right is null)
            {
                return true;
            }

            foreach ((string key, string value) in left)
            {
                if (!right.TryGetValue(key, out string? rightValue) || rightValue != value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
