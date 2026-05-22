// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Topology.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>
/// Coordinates node file history.
/// </summary>
public class NodeFileHistoryService(
    CottonDbContext _dbContext,
    ILayoutService _layouts)
{
    /// <summary>
    /// Saves version and update manifest.
    /// </summary>
    public async Task<bool> SaveVersionAndUpdateManifestAsync(
        NodeFile nodeFile,
        Guid newFileManifestId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (nodeFile.FileManifestId == newFileManifestId)
        {
            return false;
        }

        bool shouldSaveVersion = await ShouldSaveVersionAsync(nodeFile.FileManifestId, newFileManifestId, ct);

        if (shouldSaveVersion)
        {
            var trashNode = await _layouts.CreateTrashItemAsync(userId);

            var versionFile = new NodeFile
            {
                NodeId = trashNode.Id,
                OwnerId = userId,
                FileManifestId = nodeFile.FileManifestId,
                OriginalNodeFileId = nodeFile.OriginalNodeFileId,
                Metadata = nodeFile.Metadata is null || nodeFile.Metadata.Count == 0
                    ? []
                    : new Dictionary<string, string>(nodeFile.Metadata),
            };

            if (versionFile.OriginalNodeFileId == Guid.Empty)
            {
                versionFile.OriginalNodeFileId = nodeFile.Id;
                nodeFile.OriginalNodeFileId = nodeFile.Id;
            }

            versionFile.SetName(nodeFile.Name);
            await _dbContext.NodeFiles.AddAsync(versionFile, ct);
        }

        nodeFile.FileManifestId = newFileManifestId;
        return shouldSaveVersion;
    }

    private async Task<bool> ShouldSaveVersionAsync(
        Guid oldFileManifestId,
        Guid newFileManifestId,
        CancellationToken ct = default)
    {
        if (oldFileManifestId == Guid.Empty)
        {
            return false;
        }

        var oldManifest = await _dbContext.FileManifests
            .AsNoTracking()
            .Where(fm => fm.Id == oldFileManifestId)
            .Select(fm => new { fm.SizeBytes, fm.ProposedContentHash })
            .FirstOrDefaultAsync(ct);

        if (oldManifest is null || oldManifest.SizeBytes == 0)
        {
            return false;
        }

        var newManifest = await _dbContext.FileManifests
            .AsNoTracking()
            .Where(fm => fm.Id == newFileManifestId)
            .Select(fm => new { fm.ProposedContentHash })
            .FirstOrDefaultAsync(ct);

        if (newManifest is null)
        {
            return true;
        }

        return !oldManifest.ProposedContentHash.SequenceEqual(newManifest.ProposedContentHash);
    }
}
