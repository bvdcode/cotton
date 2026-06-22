// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>
/// Coordinates archive download.
/// </summary>
public sealed class ArchiveDownloadService(
    CottonDbContext _dbContext,
    ArchiveDownloadTicketStore _tickets,
    FileGraphIntegrityVerifier _fileGraphIntegrity)
{
    private const string DefaultArchiveName = "cotton-download.zip";

    /// <summary>
    /// Creates download link async.
    /// </summary>
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
        ArchiveLimitTracker? limits = request.EnforcePublicShareLimits
            ? ArchiveLimitTracker.ForPublicShare()
            : null;

        if (fileIds.Length > 0)
        {
            List<NodeFile> files = await LoadFilesAsync(fileIds, userId, cancellationToken);
            if (files.Count != fileIds.Length)
            {
                return CreateArchiveDownloadLinkResult.NotFound("One or more selected files were not found.");
            }

            foreach (NodeFile file in OrderByRequestedIds(files, fileIds, x => x.Id))
            {
                _fileGraphIntegrity.RequireValidContent(_dbContext, file, "archive.selected-file");
                CreateArchiveDownloadLinkResult? limitError = AddFileEntry(
                    entries,
                    addedFileIds,
                    uniquifier,
                    limits,
                    file,
                    file.Name);
                if (limitError is not null)
                {
                    return limitError;
                }
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
                CreateArchiveDownloadLinkResult? limitError = limits?.TryAddEntry();
                if (limitError is not null)
                {
                    return limitError;
                }

                entries.Add(new ArchiveDownloadDirectoryEntry(folderPath + "/"));
                limitError = await AddFolderEntriesAsync(
                    entries,
                    addedFileIds,
                    uniquifier,
                    limits,
                    folder.Id,
                    folderPath,
                    userId,
                    cancellationToken);
                if (limitError is not null)
                {
                    return limitError;
                }
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

    private async Task<CreateArchiveDownloadLinkResult?> AddFolderEntriesAsync(
        List<ArchiveDownloadEntry> entries,
        HashSet<Guid> addedFileIds,
        ArchivePathUniquifier uniquifier,
        ArchiveLimitTracker? limits,
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

            IQueryable<NodeFile> filesQuery = _dbContext.NodeFiles
                .Where(x => parentIds.Contains(x.NodeId) && x.OwnerId == userId && x.Node.Type == NodeType.Default)
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .OrderBy(x => x.Name)
                .AsSplitQuery();
            filesQuery = ApplyLimitProbe(filesQuery, limits);
            List<NodeFile> files = await filesQuery.ToListAsync(cancellationToken);

            foreach (NodeFile file in files)
            {
                _fileGraphIntegrity.RequireValidContent(_dbContext, file, "archive.folder-file");
                string parentPath = currentLevel[file.NodeId];
                CreateArchiveDownloadLinkResult? limitError = AddFileEntry(
                    entries,
                    addedFileIds,
                    uniquifier,
                    limits,
                    file,
                    ArchivePathUniquifier.Combine(parentPath, file.Name));
                if (limitError is not null)
                {
                    return limitError;
                }
            }

            IQueryable<Node> childFoldersQuery = _dbContext.Nodes
                .AsNoTracking()
                .Where(x => x.ParentId.HasValue && parentIds.Contains(x.ParentId.Value) && x.OwnerId == userId && x.Type == NodeType.Default)
                .OrderBy(x => x.Name);
            childFoldersQuery = ApplyLimitProbe(childFoldersQuery, limits);
            List<Node> childFolders = await childFoldersQuery.ToListAsync(cancellationToken);

            var nextLevel = new Dictionary<Guid, string>();
            foreach (Node child in childFolders)
            {
                string parentPath = currentLevel[child.ParentId!.Value];
                string childPath = uniquifier.AddDirectory(ArchivePathUniquifier.Combine(parentPath, child.Name)).TrimEnd('/');
                CreateArchiveDownloadLinkResult? limitError = limits?.TryAddEntry();
                if (limitError is not null)
                {
                    return limitError;
                }

                entries.Add(new ArchiveDownloadDirectoryEntry(childPath + "/"));
                nextLevel[child.Id] = childPath;
            }

            currentLevel = nextLevel;
        }

        return null;
    }

    private static CreateArchiveDownloadLinkResult? AddFileEntry(
        List<ArchiveDownloadEntry> entries,
        HashSet<Guid> addedFileIds,
        ArchivePathUniquifier uniquifier,
        ArchiveLimitTracker? limits,
        NodeFile file,
        string path)
    {
        if (!addedFileIds.Add(file.Id))
        {
            return null;
        }

        CreateArchiveDownloadLinkResult? limitError = limits?.TryAddEntry();
        if (limitError is not null)
        {
            addedFileIds.Remove(file.Id);
            return limitError;
        }

        string archivePath = uniquifier.AddFile(path);
        entries.Add(new ArchiveDownloadFileEntry(
            archivePath,
            file.FileManifest.SizeBytes,
            file.FileManifest.FileManifestChunks.GetChunkHashes(),
            file.FileManifest.FileManifestChunks.GetChunkLengths()));
        return null;
    }

    private async Task<List<NodeFile>> LoadFilesAsync(
        Guid[] fileIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.NodeFiles
            .Where(x => fileIds.Contains(x.Id) && x.OwnerId == userId && x.Node.Type == NodeType.Default)
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .ThenInclude(x => x.FileManifestChunks)
            .ThenInclude(x => x.Chunk)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<T> ApplyLimitProbe<T>(IQueryable<T> query, ArchiveLimitTracker? limits)
    {
        return limits is null
            ? query
            : query.Take(limits.RemainingEntries + 1);
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

    private sealed class ArchiveLimitTracker(int maxEntries)
    {
        private const int PublicShareMaxEntries = 5_000;

        private int _entryCount;

        public int RemainingEntries => Math.Max(0, maxEntries - _entryCount);

        public static ArchiveLimitTracker ForPublicShare()
        {
            return new ArchiveLimitTracker(PublicShareMaxEntries);
        }

        public CreateArchiveDownloadLinkResult? TryAddEntry()
        {
            if (_entryCount + 1 > maxEntries)
            {
                return CreateArchiveDownloadLinkResult.BadRequest(
                    $"Shared folder archive is limited to {maxEntries} entries.");
            }

            _entryCount++;
            return null;
        }
    }
}

/// <summary>
/// Represents the result of create archive download link.
/// </summary>
public sealed class CreateArchiveDownloadLinkResult
{
    private CreateArchiveDownloadLinkResult(ArchiveDownloadLinkDto? link, string? error, int statusCode)
    {
        Link = link;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the link.
    /// </summary>
    public ArchiveDownloadLinkDto? Link { get; }
    /// <summary>
    /// Gets the error.
    /// </summary>
    public string? Error { get; }
    /// <summary>
    /// Gets the status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    public static CreateArchiveDownloadLinkResult Success(ArchiveDownloadLinkDto link) => new(link, null, StatusCodes.Status200OK);
    /// <summary>
    /// Creates a bad request result.
    /// </summary>
    public static CreateArchiveDownloadLinkResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);
    /// <summary>
    /// Creates a not-found result.
    /// </summary>
    public static CreateArchiveDownloadLinkResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
}

