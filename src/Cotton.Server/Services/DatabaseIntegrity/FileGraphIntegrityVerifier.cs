// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Verifies the signed row graph required to expose file bytes outside the server.
/// </summary>
/// <remarks>
/// Listing large folders intentionally does not walk every child signature. This verifier is used at trust
/// boundaries where Cotton opens a specific file, archive entry, preview, or stream.
/// </remarks>
public sealed class FileGraphIntegrityVerifier(
    IDatabaseIntegrityVerifier _integrity,
    IDatabaseIntegrityFailureReporter _failures)
{
    private const string StructuralGraphEntityName = "file_graph";

    /// <summary>
    /// Verifies the signed file metadata graph without requiring chunk rows to be loaded.
    /// </summary>
    public void RequireValidMetadata(CottonDbContext dbContext, NodeFile nodeFile, string boundary)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(nodeFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);

        RequireNavigation(nodeFile.Node, nameof(nodeFile.Node));
        RequireNavigation(nodeFile.FileManifest, nameof(nodeFile.FileManifest));

        if (nodeFile.NodeId != nodeFile.Node.Id)
        {
            throw CreateStructuralFailure(nameof(NodeFile.NodeId), boundary, nodeFile.Id);
        }

        if (nodeFile.OwnerId != nodeFile.Node.OwnerId)
        {
            throw CreateStructuralFailure(nameof(NodeFile.OwnerId), boundary, nodeFile.Id);
        }

        if (nodeFile.FileManifestId != nodeFile.FileManifest.Id)
        {
            throw CreateStructuralFailure(nameof(NodeFile.FileManifestId), boundary, nodeFile.Id);
        }

        _integrity.RequireValid(dbContext, nodeFile.Node, boundary + ".node");
        _integrity.RequireValid(dbContext, nodeFile.FileManifest, boundary + ".manifest");
        _integrity.RequireValid(dbContext, nodeFile, boundary + ".node-file");
    }

    /// <summary>
    /// Verifies metadata plus the ordered chunk mapping used to stream file content.
    /// </summary>
    public void RequireValidContent(CottonDbContext dbContext, NodeFile nodeFile, string boundary)
    {
        RequireValidMetadata(dbContext, nodeFile, boundary);

        FileManifest manifest = nodeFile.FileManifest;
        List<FileManifestChunk> orderedChunks = [.. manifest.FileManifestChunks.OrderBy(x => x.ChunkOrder)];
        long plainSizeBytes = 0;

        for (int i = 0; i < orderedChunks.Count; i++)
        {
            FileManifestChunk manifestChunk = orderedChunks[i];
            RequireNavigation(manifestChunk.Chunk, nameof(manifestChunk.Chunk));

            if (manifestChunk.FileManifestId != manifest.Id)
            {
                throw CreateStructuralFailure(nameof(FileManifestChunk.FileManifestId), boundary, manifest.Id);
            }

            if (manifestChunk.ChunkOrder != i)
            {
                throw CreateStructuralFailure(nameof(FileManifestChunk.ChunkOrder), boundary, manifest.Id);
            }

            if (!manifestChunk.ChunkHash.SequenceEqual(manifestChunk.Chunk.Hash))
            {
                throw CreateStructuralFailure(nameof(FileManifestChunk.ChunkHash), boundary, manifest.Id);
            }

            plainSizeBytes = checked(plainSizeBytes + manifestChunk.Chunk.PlainSizeBytes);

            _integrity.RequireValid(dbContext, manifestChunk, boundary + ".manifest-chunk");
            _integrity.RequireValid(dbContext, manifestChunk.Chunk, boundary + ".chunk");
        }

        if (plainSizeBytes != manifest.SizeBytes)
        {
            throw CreateStructuralFailure(nameof(FileManifest.SizeBytes), boundary, manifest.Id);
        }
    }

    private static void RequireNavigation<T>(T? value, string navigationName)
        where T : class
    {
        if (value is null)
        {
            throw new InvalidOperationException($"File graph integrity verification requires loaded navigation '{navigationName}'.");
        }
    }

    private DatabaseIntegrityException CreateStructuralFailure(
        string fieldName,
        string boundary,
        Guid entityId)
    {
        string entityKey = entityId.ToString("D");
        _failures.Report(new DatabaseIntegrityFailure(
            StructuralGraphEntityName,
            entityKey,
            boundary + "." + fieldName,
            DateTime.UtcNow));

        return new DatabaseIntegrityException(StructuralGraphEntityName, entityKey);
    }
}
