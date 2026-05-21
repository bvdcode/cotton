// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public sealed class ArchiveDownloadService(
    CottonDbContext _dbContext,
    ArchiveDownloadTicketStore _tickets)
{
    private const string DefaultArchiveName = "cotton-download.zip";

    public async Task<CreateArchiveDownloadLinkResult> CreateDownloadLinkAsync(
        Guid userId,
        CreateArchiveDownloadLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid[] fileIds = DistinctNonEmpty(request.FileIds);
        Guid[] nodeIds = DistinctNonEmpty(request.NodeIds);
        if (fileIds.Length == 0 && nodeIds.Length == 0)
        {
            return CreateArchiveDownloadLinkResult.BadRequest("Select at least one file or folder to download.");
        }

        var uniquifier = new ArchivePathUniquifier();
        var entries = new List<ArchiveDownloadEntry>();
        var addedFileIds = new HashSet<Guid>();

        if (fileIds.Length > 0)
        {
            List<NodeFile> files = await LoadFilesAsync(fileIds, userId, cancellationToken);
            if (files.Count != fileIds.Length)
            {
                return CreateArchiveDownloadLinkResult.NotFound("One or more selected files were not found.");
            }

            foreach (NodeFile file in OrderByRequestedIds(files, fileIds, x => x.Id))
            {
                AddFileEntry(entries, addedFileIds, uniquifier, file, file.Name);
            }
        }

        if (nodeIds.Length > 0)
        {
            List<Node> folders = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => nodeIds.Contains(x.Id) && x.OwnerId == userId && x.Type == NodeType.Default)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
            if (folders.Count != nodeIds.Length)
            {
                return CreateArchiveDownloadLinkResult.NotFound("One or more selected folders were not found.");
            }

            foreach (Node folder in OrderByRequestedIds(folders, nodeIds, x => x.Id))
            {
                string folderPath = uniquifier.AddDirectory(folder.Name).TrimEnd('/');
                entries.Add(new ArchiveDownloadDirectoryEntry(folderPath + "/"));
                await AddFolderEntriesAsync(
                    entries,
                    addedFileIds,
                    uniquifier,
                    folder.Id,
                    folderPath,
                    userId,
                    cancellationToken);
            }
        }

        if (entries.Count == 0)
        {
            return CreateArchiveDownloadLinkResult.NotFound("No downloadable files were found.");
        }

        string fileName = BuildArchiveFileName(request.ArchiveName, fileIds.Length, nodeIds.Length, entries);
        long archiveSizeBytes = StoredZipArchiveWriter.CalculateLength(entries);
        var ticket = new ArchiveDownloadTicket(fileName, archiveSizeBytes, entries.Count, entries);
        string token = _tickets.Store(ticket);

        return CreateArchiveDownloadLinkResult.Success(new ArchiveDownloadLinkDto
        {
            Url = $"{Routes.V1.Archives}/{token}",
            FileName = fileName,
            SizeBytes = archiveSizeBytes,
            EntryCount = entries.Count,
        });
    }

    private async Task AddFolderEntriesAsync(
        List<ArchiveDownloadEntry> entries,
        HashSet<Guid> addedFileIds,
        ArchivePathUniquifier uniquifier,
        Guid rootFolderId,
        string rootPath,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentLevel = new Dictionary<Guid, string>
        {
            [rootFolderId] = rootPath,
        };

        while (currentLevel.Count > 0)
        {
            Guid[] parentIds = [.. currentLevel.Keys];

            List<NodeFile> files = await _dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => parentIds.Contains(x.NodeId) && x.OwnerId == userId && x.Node.Type == NodeType.Default)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .OrderBy(x => x.Name)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            foreach (NodeFile file in files)
            {
                string parentPath = currentLevel[file.NodeId];
                AddFileEntry(
                    entries,
                    addedFileIds,
                    uniquifier,
                    file,
                    ArchivePathUniquifier.Combine(parentPath, file.Name));
            }

            List<Node> childFolders = await _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.ParentId.HasValue && parentIds.Contains(x.ParentId.Value) && x.OwnerId == userId && x.Type == NodeType.Default)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var nextLevel = new Dictionary<Guid, string>();
            foreach (Node child in childFolders)
            {
                string parentPath = currentLevel[child.ParentId!.Value];
                string childPath = uniquifier.AddDirectory(ArchivePathUniquifier.Combine(parentPath, child.Name)).TrimEnd('/');
                entries.Add(new ArchiveDownloadDirectoryEntry(childPath + "/"));
                nextLevel[child.Id] = childPath;
            }

            currentLevel = nextLevel;
        }
    }

    private static void AddFileEntry(
        List<ArchiveDownloadEntry> entries,
        HashSet<Guid> addedFileIds,
        ArchivePathUniquifier uniquifier,
        NodeFile file,
        string path)
    {
        if (!addedFileIds.Add(file.Id))
        {
            return;
        }

        string archivePath = uniquifier.AddFile(path);
        entries.Add(new ArchiveDownloadFileEntry(
            archivePath,
            file.FileManifest.SizeBytes,
            file.FileManifest.FileManifestChunks.GetChunkHashes(),
            file.FileManifest.FileManifestChunks.GetChunkLengths()));
    }

    private async Task<List<NodeFile>> LoadFilesAsync(
        Guid[] fileIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => fileIds.Contains(x.Id) && x.OwnerId == userId && x.Node.Type == NodeType.Default)
            .Include(x => x.FileManifest)
            .ThenInclude(x => x.FileManifestChunks)
            .ThenInclude(x => x.Chunk)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    private static Guid[] DistinctNonEmpty(IReadOnlyList<Guid>? ids)
    {
        return ids is null
            ? []
            : [.. ids.Where(x => x != Guid.Empty).Distinct()];
    }

    private static IEnumerable<T> OrderByRequestedIds<T>(
        IReadOnlyCollection<T> items,
        Guid[] requestedIds,
        Func<T, Guid> idSelector)
    {
        var byId = items.ToDictionary(idSelector);
        foreach (Guid id in requestedIds)
        {
            yield return byId[id];
        }
    }

    private static string BuildArchiveFileName(
        string? requestedName,
        int fileCount,
        int folderCount,
        IReadOnlyList<ArchiveDownloadEntry> entries)
    {
        string baseName = requestedName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseName) && fileCount + folderCount == 1)
        {
            string firstPath = entries[0].Path.TrimEnd('/');
            int slashIndex = firstPath.IndexOf('/');
            baseName = slashIndex >= 0 ? firstPath[..slashIndex] : firstPath;
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = DefaultArchiveName;
        }

        if (baseName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^4];
        }

        if (!NameValidator.TryNormalizeAndValidate(baseName, out string normalized, out _))
        {
            normalized = "cotton-download";
        }

        return normalized + ".zip";
    }
}

public sealed class CreateArchiveDownloadLinkResult
{
    private CreateArchiveDownloadLinkResult(ArchiveDownloadLinkDto? link, string? error, int statusCode)
    {
        Link = link;
        Error = error;
        StatusCode = statusCode;
    }

    public ArchiveDownloadLinkDto? Link { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    public static CreateArchiveDownloadLinkResult Success(ArchiveDownloadLinkDto link) => new(link, null, StatusCodes.Status200OK);
    public static CreateArchiveDownloadLinkResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);
    public static CreateArchiveDownloadLinkResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
}

