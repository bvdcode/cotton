// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Topology.Abstractions;

namespace Cotton.Server.Services;

public class NodeFileHistoryService(
    CottonDbContext _dbContext,
    ILayoutService _layouts)
{
    public async Task SaveVersionAndUpdateManifestAsync(
        NodeFile nodeFile,
        Guid newFileManifestId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (nodeFile.FileManifestId == newFileManifestId)
        {
            return;
        }

        var trashNode = await _layouts.CreateTrashItemAsync(userId);

        var versionFile = new NodeFile
        {
            NodeId = trashNode.Id,
            OwnerId = userId,
            FileManifestId = nodeFile.FileManifestId,
            OriginalNodeFileId = nodeFile.OriginalNodeFileId,
        };

        if (versionFile.OriginalNodeFileId == Guid.Empty)
        {
            versionFile.OriginalNodeFileId = nodeFile.Id;
            nodeFile.OriginalNodeFileId = nodeFile.Id;
        }

        versionFile.SetName(nodeFile.Name);
        await _dbContext.NodeFiles.AddAsync(versionFile, ct);

        nodeFile.FileManifestId = newFileManifestId;
    }
}
