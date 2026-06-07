// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync;

/// <summary>
/// Reconciles local and remote file snapshots for one synchronization pair.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    private const int RunProgressReportItemInterval = 100;
    private static readonly TimeSpan RunProgressReportTimeInterval = TimeSpan.FromMilliseconds(250);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly ILocalFileScanner _localScanner;
    private readonly ILocalFileContentHasher? _localContentHasher;
    private readonly ILocalFileMetadataTreeScanner? _localMetadataTreeScanner;
    private readonly ILocalFileMetadataTreeLookupScanner? _localMetadataTreeLookupScanner;
    private readonly ILocalTreeScanner? _localTreeScanner;
    private readonly IRemoteDirectorySynchronizer? _remoteDirectories;
    private readonly IRemoteTreeCrawler _remoteCrawler;
    private readonly IRemoteTreeLookupCrawler? _remoteLookupCrawler;
    private readonly IRemoteFileSynchronizer _remoteFiles;
    private readonly ISyncStateStore _stateStore;
    private readonly ILocalFileSyncWriter _localWriter;
    private readonly ILogger<SyncEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEngine" /> class.
    /// </summary>
    public SyncEngine(
        ILocalFileScanner localScanner,
        IRemoteTreeCrawler remoteCrawler,
        IRemoteFileSynchronizer remoteFiles,
        ISyncStateStore stateStore,
        ILocalFileSyncWriter? localWriter = null,
        IRemoteDirectorySynchronizer? remoteDirectories = null,
        ILogger<SyncEngine>? logger = null)
    {
        _localScanner = localScanner ?? throw new ArgumentNullException(nameof(localScanner));
        _localContentHasher = localScanner as ILocalFileContentHasher;
        _localMetadataTreeScanner = localScanner as ILocalFileMetadataTreeScanner;
        _localMetadataTreeLookupScanner = localScanner as ILocalFileMetadataTreeLookupScanner;
        _localTreeScanner = localScanner as ILocalTreeScanner;
        _remoteCrawler = remoteCrawler ?? throw new ArgumentNullException(nameof(remoteCrawler));
        _remoteLookupCrawler = remoteCrawler as IRemoteTreeLookupCrawler;
        _remoteFiles = remoteFiles ?? throw new ArgumentNullException(nameof(remoteFiles));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _localWriter = localWriter ?? new AtomicLocalFileSyncWriter();
        _remoteDirectories = remoteDirectories;
        _logger = logger ?? NullLogger<SyncEngine>.Instance;
    }

    /// <inheritdoc />
    public async Task<SyncRunResult> RunOnceAsync(
        SyncPair syncPair,
        SyncRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncPair);
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPair.SyncPairId);
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPair.LocalRootPath);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= new SyncRunOptions();
        ValidateOptions(options);
        DateTime startedAtUtc = DateTime.UtcNow;
        ReportRunProgress(options, SyncRunProgressStage.ScanningLocal, 0, null, null, startedAtUtc);
        _logger.LogInformation("Starting sync pass for pair {SyncPairId}.", syncPair.SyncPairId);
        await _stateStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        SyncTreeLookups treeLookups = await ScanTreesAndBuildLookupsAsync(syncPair, options, startedAtUtc, cancellationToken)
            .ConfigureAwait(false);
        (Dictionary<string, SyncStateEntry> directoryStateByPath, Dictionary<string, SyncStateEntry> stateByPath) =
            await LoadStateByPathAsync(syncPair.SyncPairId, cancellationToken).ConfigureAwait(false);
        var result = new SyncRunResult();

        Dictionary<string, LocalDirectorySnapshot> localDirectoriesByPath = treeLookups.LocalDirectoriesByPath;
        Dictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath = treeLookups.RemoteDirectoriesByPath;
        Dictionary<string, LocalFileSnapshot> localByPath = treeLookups.LocalFilesByPath;
        Dictionary<string, RemoteFileSnapshot> remoteByPath = treeLookups.RemoteFilesByPath;
        IReadOnlyList<string> directoryPathKeys = BuildDirectoryPathKeys(
            localDirectoriesByPath.Keys,
            remoteDirectoriesByPath.Keys,
            directoryStateByPath.Keys);
        ReportRunProgress(options, SyncRunProgressStage.ReconcilingDirectories, 0, directoryPathKeys.Count, null, startedAtUtc);
        await ReconcileDirectoriesWithoutBaselineAsync(
            syncPair,
            options,
            result,
            directoryPathKeys,
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath,
            treeLookups.RemoteRootNode,
            startedAtUtc,
            cancellationToken).ConfigureAwait(false);

        await EnsureLocalContentHashesForStateFilesAsync(localByPath, stateByPath, options, startedAtUtc, cancellationToken)
            .ConfigureAwait(false);

        bool hasLocalDirectoryDeleteCandidates = HasLocalDirectoryDeleteCandidates(
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath);
        bool hasRemoteDirectoryDeleteCandidates = HasRemoteDirectoryDeleteCandidates(
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath);
        DirectoryContentIndex localDirectoryContentIndex = hasLocalDirectoryDeleteCandidates
            ? DirectoryContentIndex.Create(localDirectoriesByPath.Keys, localByPath.Keys)
            : DirectoryContentIndex.Empty;
        DirectoryContentIndex remoteDirectoryContentIndex = hasRemoteDirectoryDeleteCandidates
            ? DirectoryContentIndex.Create(remoteDirectoriesByPath.Keys, remoteByPath.Keys)
            : DirectoryContentIndex.Empty;

        SyncDeleteGuard deleteGuard = BuildDeleteGuard(
            options,
            localByPath,
            remoteByPath,
            stateByPath,
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath,
            localDirectoryContentIndex,
            remoteDirectoryContentIndex);

        if (hasLocalDirectoryDeleteCandidates || hasRemoteDirectoryDeleteCandidates)
        {
            await ReconcileDirectoryDeletesAsync(
                syncPair,
                options,
                result,
                deleteGuard,
                directoryPathKeys,
                localDirectoriesByPath,
                remoteDirectoriesByPath,
                directoryStateByPath,
                localDirectoryContentIndex,
                remoteDirectoryContentIndex,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<string> pathKeys = BuildPathKeys(localByPath.Keys, remoteByPath.Keys, stateByPath.Keys);
        EnsureEnoughLocalFreeSpaceForPlannedDownloads(
            syncPair.LocalRootPath,
            pathKeys,
            localByPath,
            remoteByPath,
            stateByPath);
        long plannedTransferBytesTotal = CalculatePlannedTransferBytesTotal(
            pathKeys,
            localByPath,
            remoteByPath,
            stateByPath);
        long completedTransferBytes = 0;
        int filesCompleted = 0;
        DateTime? lastFileRunProgressReportedAtUtc = null;
        ReportRunProgress(
            options,
            SyncRunProgressStage.ReconcilingFiles,
            filesCompleted,
            pathKeys.Count,
            null,
            startedAtUtc,
            bytesCompleted: completedTransferBytes,
            bytesTotal: plannedTransferBytesTotal);
        foreach (string key in pathKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            localByPath.TryGetValue(key, out LocalFileSnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
            stateByPath.TryGetValue(key, out SyncStateEntry? state);
            string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state?.RelativePath ?? key;
            long plannedTransferBytes = CalculatePlannedTransferBytes(key, localByPath, remoteByPath, stateByPath);
            ReportItemRunProgress(
                options,
                SyncRunProgressStage.ReconcilingFiles,
                filesCompleted,
                pathKeys.Count,
                relativePath,
                startedAtUtc,
                ref lastFileRunProgressReportedAtUtc,
                bytesCompleted: completedTransferBytes,
                bytesTotal: plannedTransferBytesTotal);

            if (state is null)
            {
                await ReconcileWithoutBaselineAsync(syncPair, options, result, relativePath, local, remote, cancellationToken).ConfigureAwait(false);
                filesCompleted++;
                completedTransferBytes += plannedTransferBytes;
                ReportItemRunProgress(
                    options,
                    SyncRunProgressStage.ReconcilingFiles,
                    filesCompleted,
                    pathKeys.Count,
                    relativePath,
                    startedAtUtc,
                    ref lastFileRunProgressReportedAtUtc,
                    bytesCompleted: completedTransferBytes,
                    bytesTotal: plannedTransferBytesTotal);
                continue;
            }

            await ReconcileWithBaselineAsync(syncPair, options, result, deleteGuard, state, relativePath, local, remote, cancellationToken)
                .ConfigureAwait(false);
            filesCompleted++;
            completedTransferBytes += plannedTransferBytes;
            ReportItemRunProgress(
                options,
                SyncRunProgressStage.ReconcilingFiles,
                filesCompleted,
                pathKeys.Count,
                relativePath,
                startedAtUtc,
                ref lastFileRunProgressReportedAtUtc,
                bytesCompleted: completedTransferBytes,
                bytesTotal: plannedTransferBytesTotal);
        }

        ReportRunProgress(
            options,
            SyncRunProgressStage.Completed,
            filesCompleted,
            pathKeys.Count,
            null,
            startedAtUtc,
            isCompleted: true,
            bytesCompleted: plannedTransferBytesTotal,
            bytesTotal: plannedTransferBytesTotal);
        _logger.LogInformation(
            "Completed sync pass for pair {SyncPairId} with {ActivityCount} activities.",
            syncPair.SyncPairId,
            result.TotalActivityCount);
        return result;
    }

    private async Task<SyncTreeLookups> ScanTreesAndBuildLookupsAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        LocalTreeLookupSnapshot? localTreeLookups = await ScanLocalTreeLookupsAsync(
                syncPair.LocalRootPath,
                options,
                startedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        LocalTreeSnapshot? localTree = null;
        if (localTreeLookups is null)
        {
            localTree = await ScanLocalTreeAsync(syncPair.LocalRootPath, options, startedAtUtc, cancellationToken)
                .ConfigureAwait(false);
        }

        RemoteTreeLookupSnapshot? remoteTreeLookups = await ScanRemoteTreeLookupsAsync(
                syncPair.RemoteRootNodeId,
                options,
                startedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        RemoteTreeSnapshot? remoteTree = null;
        if (remoteTreeLookups is null)
        {
            remoteTree = await ScanRemoteTreeAsync(syncPair.RemoteRootNodeId, options, startedAtUtc, cancellationToken)
                .ConfigureAwait(false);
        }

        Dictionary<string, LocalDirectorySnapshot> localDirectoriesByPath = localTreeLookups?.DirectoriesByPath
            ?? ToDictionary(localTree!.Directories, directory => directory.RelativePath);
        Dictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath = remoteTreeLookups?.DirectoriesByPath
            ?? ToDictionary(remoteTree!.Directories, directory => directory.RelativePath);
        Dictionary<string, LocalFileSnapshot> localByPath = localTreeLookups?.FilesByPath
            ?? ToDictionary(localTree!.Files, file => file.RelativePath);
        Dictionary<string, RemoteFileSnapshot> remoteByPath = remoteTreeLookups?.FilesByPath
            ?? ToDictionary(remoteTree!.Files, file => file.RelativePath);
        ThrowIfPathKindCollisions(
            localDirectoriesByPath,
            localByPath,
            directory => directory.RelativePath,
            file => file.RelativePath);
        ThrowIfPathKindCollisions(
            remoteDirectoriesByPath,
            remoteByPath,
            directory => directory.RelativePath,
            file => file.RelativePath);
        return new SyncTreeLookups(
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            localByPath,
            remoteByPath,
            remoteTreeLookups?.RootNode ?? remoteTree!.RootNode);
    }

    private async Task<(Dictionary<string, SyncStateEntry> DirectoryStateByPath, Dictionary<string, SyncStateEntry> FileStateByPath)> LoadStateByPathAsync(
        string syncPairId,
        CancellationToken cancellationToken)
    {
        var directoryStateByPath = new Dictionary<string, SyncStateEntry>(PathComparer);
        var fileStateByPath = new Dictionary<string, SyncStateEntry>(PathComparer);
        await foreach (SyncStateEntry entry in _stateStore.LoadPairEntriesAsync(syncPairId, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            if (SyncPathIgnoreRules.ShouldIgnore(entry.RelativePath))
            {
                await _stateStore.DeleteAsync(syncPairId, entry.RelativePath, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string key = SyncPath.ToKey(entry.RelativePath);
            switch (entry.Kind)
            {
                case SyncEntryKind.Directory:
                    directoryStateByPath.Add(key, entry);
                    break;
                case SyncEntryKind.File:
                    fileStateByPath.Add(key, entry);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "Unknown sync state entry kind.");
            }
        }

        return (directoryStateByPath, fileStateByPath);
    }

    private async Task EnsureLocalContentHashesForStateFilesAsync(
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        SyncRunOptions options,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (stateByPath.Count == 0)
        {
            return;
        }

        int filesTotal = stateByPath.Count(state => localByPath.ContainsKey(state.Key));
        if (filesTotal == 0)
        {
            return;
        }

        int filesCompleted = 0;
        DateTime? lastReportedAtUtc = null;
        ReportItemRunProgress(
            options,
            SyncRunProgressStage.ScanningLocal,
            filesCompleted,
            filesTotal,
            currentPath: null,
            startedAtUtc,
            ref lastReportedAtUtc);

        foreach (KeyValuePair<string, SyncStateEntry> state in stateByPath)
        {
            if (localByPath.TryGetValue(state.Key, out LocalFileSnapshot? local))
            {
                ReportItemRunProgress(
                    options,
                    SyncRunProgressStage.ScanningLocal,
                    filesCompleted,
                    filesTotal,
                    local.RelativePath,
                    startedAtUtc,
                    ref lastReportedAtUtc);
                await EnsureLocalContentHashForBaselineComparisonAsync(local, state.Value, cancellationToken).ConfigureAwait(false);
                filesCompleted++;
                ReportItemRunProgress(
                    options,
                    SyncRunProgressStage.ScanningLocal,
                    filesCompleted,
                    filesTotal,
                    local.RelativePath,
                    startedAtUtc,
                    ref lastReportedAtUtc);
            }
        }
    }

    private async Task<LocalTreeSnapshot> ScanLocalTreeAsync(
        string localRootPath,
        SyncRunOptions options,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_localMetadataTreeScanner is ILocalFileMetadataTreeProgressScanner progressScanner && _localContentHasher is not null)
        {
            return await progressScanner
                .ScanTreeMetadataAsync(
                    localRootPath,
                    new LocalTreeScanProgressReporter(options, startedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_localMetadataTreeScanner is not null && _localContentHasher is not null)
        {
            return await _localMetadataTreeScanner.ScanTreeMetadataAsync(localRootPath, cancellationToken).ConfigureAwait(false);
        }

        if (_localTreeScanner is not null)
        {
            return await _localTreeScanner.ScanTreeAsync(localRootPath, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<LocalFileSnapshot> files = await _localScanner.ScanAsync(localRootPath, cancellationToken).ConfigureAwait(false);
        return new LocalTreeSnapshot
        {
            Files = files.ToList(),
        };
    }

    private async Task<LocalTreeLookupSnapshot?> ScanLocalTreeLookupsAsync(
        string localRootPath,
        SyncRunOptions options,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_localMetadataTreeLookupScanner is null || _localContentHasher is null)
        {
            return null;
        }

        return await _localMetadataTreeLookupScanner
            .ScanTreeMetadataLookupsAsync(
                localRootPath,
                new LocalTreeScanProgressReporter(options, startedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<RemoteTreeSnapshot> ScanRemoteTreeAsync(
        Guid remoteRootNodeId,
        SyncRunOptions options,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        ReportRunProgress(options, SyncRunProgressStage.ScanningRemote, 0, null, null, startedAtUtc);
        if (_remoteCrawler is IRemoteTreeProgressCrawler progressCrawler)
        {
            return await progressCrawler
                .CrawlAsync(
                    remoteRootNodeId,
                    new RemoteTreeScanProgressReporter(options, startedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await _remoteCrawler.CrawlAsync(remoteRootNodeId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RemoteTreeLookupSnapshot?> ScanRemoteTreeLookupsAsync(
        Guid remoteRootNodeId,
        SyncRunOptions options,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_remoteLookupCrawler is null)
        {
            return null;
        }

        ReportRunProgress(options, SyncRunProgressStage.ScanningRemote, 0, null, null, startedAtUtc);
        return await _remoteLookupCrawler
            .CrawlLookupsAsync(
                remoteRootNodeId,
                new RemoteTreeScanProgressReporter(options, startedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ReconcileDirectoriesWithoutBaselineAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        IReadOnlyList<string> pathKeys,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localByPath,
        IDictionary<string, RemoteDirectorySnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        NodeDto remoteRootNode,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        int foldersCompleted = 0;
        DateTime? lastDirectoryRunProgressReportedAtUtc = null;
        foreach (string key in pathKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            localByPath.TryGetValue(key, out LocalDirectorySnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteDirectorySnapshot? remote);
            stateByPath.TryGetValue(key, out SyncStateEntry? state);
            string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state?.RelativePath ?? key;
            ReportItemRunProgress(
                options,
                SyncRunProgressStage.ReconcilingDirectories,
                foldersCompleted,
                pathKeys.Count,
                relativePath,
                startedAtUtc,
                ref lastDirectoryRunProgressReportedAtUtc);
            if (state is not null)
            {
                foldersCompleted++;
                ReportItemRunProgress(
                    options,
                    SyncRunProgressStage.ReconcilingDirectories,
                    foldersCompleted,
                    pathKeys.Count,
                    relativePath,
                    startedAtUtc,
                    ref lastDirectoryRunProgressReportedAtUtc);
                continue;
            }

            if (local is null && remote is not null)
            {
                await _localWriter.CreateDirectoryAsync(syncPair.LocalRootPath, relativePath, cancellationToken).ConfigureAwait(false);
                await _stateStore.UpsertAsync(BuildDirectoryBaseline(syncPair, relativePath, remote.Node), cancellationToken)
                    .ConfigureAwait(false);
                Report(result, options, SyncActivityKind.Downloaded, relativePath, "Created local folder.");
                foldersCompleted++;
                ReportItemRunProgress(
                    options,
                    SyncRunProgressStage.ReconcilingDirectories,
                    foldersCompleted,
                    pathKeys.Count,
                    relativePath,
                    startedAtUtc,
                    ref lastDirectoryRunProgressReportedAtUtc);
                continue;
            }

            if (local is not null && remote is null && _remoteDirectories is not null)
            {
                string parentPath = GetParentPath(relativePath);
                string parentKey = string.IsNullOrEmpty(parentPath) ? string.Empty : SyncPath.ToKey(parentPath);
                if (!TryGetRemoteDirectoryNodeId(remoteByPath, parentKey, remoteRootNode.Id, out Guid parentNodeId))
                {
                    foldersCompleted++;
                    ReportItemRunProgress(
                        options,
                        SyncRunProgressStage.ReconcilingDirectories,
                        foldersCompleted,
                        pathKeys.Count,
                        relativePath,
                        startedAtUtc,
                        ref lastDirectoryRunProgressReportedAtUtc);
                    continue;
                }

                NodeDto created = await _remoteDirectories
                    .CreateDirectoryAsync(parentNodeId, GetFileName(relativePath), cancellationToken)
                    .ConfigureAwait(false);
                var createdSnapshot = new RemoteDirectorySnapshot
                {
                    RelativePath = relativePath,
                    Node = created,
                };
                remoteByPath[SyncPath.ToKey(relativePath)] = createdSnapshot;
                await _stateStore.UpsertAsync(BuildDirectoryBaseline(syncPair, relativePath, created), cancellationToken)
                    .ConfigureAwait(false);
                Report(result, options, SyncActivityKind.Uploaded, relativePath, "Created remote folder.");
                foldersCompleted++;
                ReportItemRunProgress(
                    options,
                    SyncRunProgressStage.ReconcilingDirectories,
                    foldersCompleted,
                    pathKeys.Count,
                    relativePath,
                    startedAtUtc,
                    ref lastDirectoryRunProgressReportedAtUtc);
                continue;
            }

            if (local is not null && remote is not null)
            {
                await _stateStore.UpsertAsync(BuildDirectoryBaseline(syncPair, relativePath, remote.Node), cancellationToken)
                    .ConfigureAwait(false);
            }

            foldersCompleted++;
            ReportItemRunProgress(
                options,
                SyncRunProgressStage.ReconcilingDirectories,
                foldersCompleted,
                pathKeys.Count,
                relativePath,
                startedAtUtc,
                ref lastDirectoryRunProgressReportedAtUtc);
        }
    }

    private static bool TryGetRemoteDirectoryNodeId(
        IDictionary<string, RemoteDirectorySnapshot> remoteByPath,
        string key,
        Guid remoteRootNodeId,
        out Guid nodeId)
    {
        if (string.IsNullOrEmpty(key))
        {
            nodeId = remoteRootNodeId;
            return true;
        }

        if (remoteByPath.TryGetValue(key, out RemoteDirectorySnapshot? remote))
        {
            nodeId = remote.Node.Id;
            return true;
        }

        nodeId = Guid.Empty;
        return false;
    }

    private async Task ReconcileDirectoryDeletesAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        IReadOnlyList<string> pathKeys,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        DirectoryContentIndex localDirectoryContentIndex,
        DirectoryContentIndex remoteDirectoryContentIndex,
        CancellationToken cancellationToken)
    {
        foreach (string key in EnumerateDirectoryDeleteKeys(pathKeys))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!stateByPath.TryGetValue(key, out SyncStateEntry? state))
            {
                continue;
            }

            localByPath.TryGetValue(key, out LocalDirectorySnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteDirectorySnapshot? remote);
            string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state.RelativePath;

            if (local is null && remote is null)
            {
                await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (local is null && remote is not null)
            {
                await DeleteRemoteDirectoryAsync(
                    syncPair,
                    options,
                    result,
                    deleteGuard,
                    relativePath,
                    remote,
                    remoteDirectoryContentIndex,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (remote is null && local is not null)
            {
                await DeleteLocalDirectoryAsync(
                    syncPair,
                    options,
                    result,
                    deleteGuard,
                    relativePath,
                    localDirectoryContentIndex,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReconcileWithoutBaselineAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        string relativePath,
        LocalFileSnapshot? local,
        RemoteFileSnapshot? remote,
        CancellationToken cancellationToken)
    {
        if (local is not null && remote is null)
        {
            await UploadAsync(syncPair, options, result, relativePath, local, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (local is null && remote is not null)
        {
            await DownloadAsync(syncPair, options, result, relativePath, remote.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (local is not null && remote is not null)
        {
            await EnsureLocalContentHashAsync(local, cancellationToken).ConfigureAwait(false);
            if (ContentMatches(local.ContentHash, remote.File.ContentHash))
            {
                await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, local.SizeBytes, remote.File), cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await PreserveConflictAsync(syncPair, options, result, relativePath, local, remote.File, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileWithBaselineAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        SyncStateEntry state,
        string relativePath,
        LocalFileSnapshot? local,
        RemoteFileSnapshot? remote,
        CancellationToken cancellationToken)
    {
        if (local is not null)
        {
            await EnsureLocalContentHashForBaselineComparisonAsync(local, state, cancellationToken).ConfigureAwait(false);
        }

        bool localDeleted = local is null && !string.IsNullOrWhiteSpace(state.LocalContentHash);
        bool remoteDeleted = remote is null && state.RemoteFileId.HasValue;
        bool localChanged = local is not null && !ContentMatches(local.ContentHash, state.LocalContentHash);
        bool remoteChanged = remote is not null && !RemoteMatchesBaseline(remote.File, state);
        bool baselineDiverged = !ContentMatches(state.LocalContentHash, state.RemoteContentHash);

        if (local is null && remote is null)
        {
            await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (local is not null && remote is not null && ContentMatches(local.ContentHash, remote.File.ContentHash))
        {
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, local.SizeBytes, remote.File), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (baselineDiverged)
        {
            if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
            {
                return;
            }

            await PreserveConflictAsync(syncPair, options, result, relativePath, local, remote?.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
        {
            return;
        }

        if (localDeleted && remoteDeleted)
        {
            await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (localDeleted && !remoteChanged && remote is not null)
        {
            await DeleteRemoteAsync(syncPair, options, result, deleteGuard, relativePath, remote.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (remoteDeleted && !localChanged && local is not null)
        {
            await DeleteLocalAsync(syncPair, options, result, deleteGuard, relativePath, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (localDeleted && remoteChanged && remote is not null)
        {
            await PreserveConflictAsync(syncPair, options, result, relativePath, null, remote.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (remoteDeleted && localChanged && local is not null)
        {
            await PreserveConflictAsync(syncPair, options, result, relativePath, local, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (localChanged && !remoteChanged && local is not null)
        {
            await UploadAsync(syncPair, options, result, relativePath, local, remote?.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (remoteChanged && !localChanged && remote is not null)
        {
            await DownloadAsync(syncPair, options, result, relativePath, remote.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        await PreserveConflictAsync(syncPair, options, result, relativePath, local, remote?.File, cancellationToken).ConfigureAwait(false);
    }

    private async Task UploadAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        string relativePath,
        LocalFileSnapshot local,
        NodeFileManifestDto? existingRemoteFile,
        CancellationToken cancellationToken)
    {
        NodeFileManifestDto uploaded;
        try
        {
            uploaded = await UploadFileWithProgressAsync(
                syncPair.RemoteRootNodeId,
                relativePath,
                local,
                existingRemoteFile,
                options,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (existingRemoteFile is not null && IsRemotePreconditionFailed(exception))
        {
            NodeFileManifestDto? latestRemoteFile = await FindLatestRemoteFileAsync(syncPair, relativePath, cancellationToken).ConfigureAwait(false);
            await PreserveConflictAsync(
                syncPair,
                options,
                result,
                relativePath,
                local,
                latestRemoteFile ?? existingRemoteFile,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string localContentHash = ResolveUploadedLocalContentHash(local, uploaded);
        local.ContentHash = localContentHash;
        await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, localContentHash, local.LastWriteUtc, local.SizeBytes, uploaded), cancellationToken)
            .ConfigureAwait(false);
        Report(result, options, SyncActivityKind.Uploaded, relativePath, null);
    }

    private async Task DownloadAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        string relativePath,
        NodeFileManifestDto remoteFile,
        CancellationToken cancellationToken)
    {
        EnsureEnoughLocalFreeSpace(syncPair.LocalRootPath, relativePath, remoteFile.SizeBytes);
        await _localWriter.WriteFileAsync(
            syncPair.LocalRootPath,
            relativePath,
            (stream, token) => DownloadAndVerifyFileAsync(remoteFile, relativePath, options, stream, token),
            remoteFile.UpdatedAt == default ? null : remoteFile.UpdatedAt,
            cancellationToken).ConfigureAwait(false);
        await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, remoteFile.ContentHash, remoteFile.UpdatedAt, remoteFile.SizeBytes, remoteFile), cancellationToken)
            .ConfigureAwait(false);
        Report(result, options, SyncActivityKind.Downloaded, relativePath, null);
    }

    private async Task DeleteRemoteAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        string relativePath,
        NodeFileManifestDto remoteFile,
        CancellationToken cancellationToken)
    {
        if (!deleteGuard.CanDeleteRemote(out string? details))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, details, requiresUserAction: true);
            return;
        }

        try
        {
            await _remoteFiles.DeleteFileAsync(
                remoteFile.Id,
                options.DeleteRemotePermanently,
                remoteFile.ETag,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (IsRemotePreconditionFailed(exception))
        {
            NodeFileManifestDto? latestRemoteFile = await FindLatestRemoteFileAsync(syncPair, relativePath, cancellationToken).ConfigureAwait(false);
            await PreserveConflictAsync(
                syncPair,
                options,
                result,
                relativePath,
                local: null,
                remoteFile: latestRemoteFile ?? remoteFile,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedRemote, relativePath, null);
    }

    private async Task DeleteLocalAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (!deleteGuard.CanDeleteLocal(out string? details))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, details, requiresUserAction: true);
            return;
        }

        await _localWriter.DeleteFileAsync(syncPair.LocalRootPath, relativePath, cancellationToken).ConfigureAwait(false);
        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedLocal, relativePath, null);
    }

    private async Task DeleteRemoteDirectoryAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        string relativePath,
        RemoteDirectorySnapshot remote,
        DirectoryContentIndex remoteDirectoryContentIndex,
        CancellationToken cancellationToken)
    {
        if (_remoteDirectories is null)
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, "Remote folder delete is not available.");
            return;
        }

        if (remoteDirectoryContentIndex.HasChildren(relativePath))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, "Remote folder delete skipped because the folder is not empty.");
            return;
        }

        if (!deleteGuard.CanDeleteRemote(out string? details))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, details, requiresUserAction: true);
            return;
        }

        await _remoteDirectories
            .DeleteDirectoryAsync(remote.Node.Id, options.DeleteRemotePermanently, cancellationToken)
            .ConfigureAwait(false);
        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedRemote, relativePath, "Deleted remote folder.");
    }

    private async Task DeleteLocalDirectoryAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        string relativePath,
        DirectoryContentIndex localDirectoryContentIndex,
        CancellationToken cancellationToken)
    {
        if (localDirectoryContentIndex.HasChildren(relativePath))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, "Local folder delete skipped because the folder is not empty.");
            return;
        }

        if (!deleteGuard.CanDeleteLocal(out string? details))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, details, requiresUserAction: true);
            return;
        }

        await _localWriter.DeleteDirectoryAsync(syncPair.LocalRootPath, relativePath, cancellationToken).ConfigureAwait(false);
        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedLocal, relativePath, "Deleted local folder.");
    }

    private async Task PreserveConflictAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        string relativePath,
        LocalFileSnapshot? local,
        NodeFileManifestDto? remoteFile,
        CancellationToken cancellationToken)
    {
        string? details = null;
        if (local is not null && remoteFile is not null)
        {
            await EnsureLocalContentHashAsync(local, cancellationToken).ConfigureAwait(false);
            string conflictPath = _localWriter.CreateConflictRelativePath(syncPair.LocalRootPath, relativePath, DateTime.UtcNow);
            await _localWriter.WriteFileAsync(
                syncPair.LocalRootPath,
                conflictPath,
                (stream, token) => DownloadAndVerifyFileAsync(remoteFile, relativePath, options, stream, token),
                remoteFile.UpdatedAt == default ? null : remoteFile.UpdatedAt,
                cancellationToken).ConfigureAwait(false);
            details = "Remote version saved as " + conflictPath;
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, local.SizeBytes, remoteFile), cancellationToken)
                .ConfigureAwait(false);
        }
        else if (local is not null)
        {
            NodeFileManifestDto uploaded = await UploadFileWithProgressAsync(
                syncPair.RemoteRootNodeId,
                relativePath,
                local,
                null,
                options,
                cancellationToken).ConfigureAwait(false);
            details = "Remote deletion conflicted with local change; local version was uploaded again.";
            string localContentHash = ResolveUploadedLocalContentHash(local, uploaded);
            local.ContentHash = localContentHash;
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, localContentHash, local.LastWriteUtc, local.SizeBytes, uploaded), cancellationToken)
                .ConfigureAwait(false);
        }
        else if (remoteFile is not null)
        {
            await _localWriter.WriteFileAsync(
                syncPair.LocalRootPath,
                relativePath,
                (stream, token) => DownloadAndVerifyFileAsync(remoteFile, relativePath, options, stream, token),
                remoteFile.UpdatedAt == default ? null : remoteFile.UpdatedAt,
                cancellationToken).ConfigureAwait(false);
            details = "Local deletion conflicted with remote change; remote version was restored locally.";
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, remoteFile.ContentHash, remoteFile.UpdatedAt, remoteFile.SizeBytes, remoteFile), cancellationToken)
                .ConfigureAwait(false);
        }

        Report(result, options, SyncActivityKind.Conflict, relativePath, details);
    }

    private async Task<NodeFileManifestDto> UploadFileWithProgressAsync(
        Guid rootNodeId,
        string relativePath,
        LocalFileSnapshot local,
        NodeFileManifestDto? existingRemoteFile,
        SyncRunOptions options,
        CancellationToken cancellationToken)
    {
        if (_remoteFiles is IRemoteFileTransferProgressSynchronizer progressSynchronizer)
        {
            return await progressSynchronizer.UploadFileAsync(
                rootNodeId,
                relativePath,
                local,
                existingRemoteFile,
                options.TransferProgress,
                cancellationToken).ConfigureAwait(false);
        }

        ReportTransfer(
            options,
            SyncTransferDirection.Upload,
            relativePath,
            transferredBytes: 0,
            totalBytes: local.SizeBytes);
        NodeFileManifestDto uploaded = await _remoteFiles.UploadFileAsync(
            rootNodeId,
            relativePath,
            local,
            existingRemoteFile,
            cancellationToken).ConfigureAwait(false);
        ReportTransfer(
            options,
            SyncTransferDirection.Upload,
            relativePath,
            local.SizeBytes,
            local.SizeBytes,
            isCompleted: true);
        return uploaded;
    }

    private async Task DownloadFileWithProgressAsync(
        NodeFileManifestDto remoteFile,
        string relativePath,
        SyncRunOptions options,
        Stream destination,
        CancellationToken cancellationToken)
    {
        if (_remoteFiles is IRemoteFileTransferProgressSynchronizer progressSynchronizer)
        {
            await progressSynchronizer.DownloadFileAsync(
                remoteFile.Id,
                relativePath,
                remoteFile.SizeBytes,
                destination,
                options.TransferProgress,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        ReportTransfer(
            options,
            SyncTransferDirection.Download,
            relativePath,
            transferredBytes: 0,
            totalBytes: remoteFile.SizeBytes);
        await _remoteFiles.DownloadFileAsync(remoteFile.Id, destination, cancellationToken).ConfigureAwait(false);
        ReportTransfer(
            options,
            SyncTransferDirection.Download,
            relativePath,
            remoteFile.SizeBytes,
            remoteFile.SizeBytes,
            isCompleted: true);
    }

    private async Task DownloadAndVerifyFileAsync(
        NodeFileManifestDto remoteFile,
        string relativePath,
        SyncRunOptions options,
        Stream destination,
        CancellationToken cancellationToken)
    {
        await using var verifiedDestination = new VerifyingDownloadStream(destination);
        await DownloadFileWithProgressAsync(remoteFile, relativePath, options, verifiedDestination, cancellationToken)
            .ConfigureAwait(false);
        verifiedDestination.Verify(remoteFile.ContentHash, remoteFile.SizeBytes, relativePath);
    }

    private async Task<NodeFileManifestDto?> FindLatestRemoteFileAsync(
        SyncPair syncPair,
        string relativePath,
        CancellationToken cancellationToken)
    {
        RemoteTreeSnapshot latestTree = await _remoteCrawler.CrawlAsync(syncPair.RemoteRootNodeId, cancellationToken).ConfigureAwait(false);
        string key = SyncPath.ToKey(relativePath);
        return latestTree.Files.FirstOrDefault(file => PathComparer.Equals(SyncPath.ToKey(file.RelativePath), key))?.File;
    }

    private static SyncStateEntry BuildBaseline(
        SyncPair syncPair,
        string relativePath,
        string? localContentHash,
        DateTime? localLastWriteUtc,
        long? localSizeBytes,
        NodeFileManifestDto? remoteFile)
    {
        return new SyncStateEntry
        {
            SyncPairId = syncPair.SyncPairId,
            RelativePath = SyncPath.Normalize(relativePath),
            Kind = SyncEntryKind.File,
            LocalContentHash = localContentHash,
            LocalLastWriteUtc = localLastWriteUtc?.ToUniversalTime(),
            LocalSizeBytes = localSizeBytes,
            RemoteFileId = remoteFile?.Id,
            RemoteNodeId = remoteFile?.NodeId,
            RemoteContentHash = remoteFile?.ContentHash,
            RemoteETag = remoteFile?.ETag,
            SyncedAtUtc = DateTime.UtcNow,
        };
    }

    private static SyncStateEntry BuildDirectoryBaseline(
        SyncPair syncPair,
        string relativePath,
        NodeDto remoteNode)
    {
        return new SyncStateEntry
        {
            SyncPairId = syncPair.SyncPairId,
            RelativePath = SyncPath.Normalize(relativePath),
            Kind = SyncEntryKind.Directory,
            RemoteNodeId = remoteNode.Id,
            SyncedAtUtc = DateTime.UtcNow,
        };
    }

    private static int GetPathDepth(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? 0
            : relativePath.Count(static character => character == '/') + 1;
    }

    private static string GetParentPath(string relativePath)
    {
        string normalized = SyncPath.Normalize(relativePath);
        int lastSlashIndex = normalized.LastIndexOf('/');
        return lastSlashIndex < 0 ? string.Empty : normalized[..lastSlashIndex];
    }

    private static string GetFileName(string relativePath)
    {
        string normalized = SyncPath.Normalize(relativePath);
        int lastSlashIndex = normalized.LastIndexOf('/');
        return lastSlashIndex < 0 ? normalized : normalized[(lastSlashIndex + 1)..];
    }

    private static bool RemoteMatchesBaseline(NodeFileManifestDto remoteFile, SyncStateEntry state)
    {
        if (!string.IsNullOrWhiteSpace(state.RemoteContentHash))
        {
            return ContentMatches(remoteFile.ContentHash, state.RemoteContentHash);
        }

        if (!string.IsNullOrWhiteSpace(state.RemoteETag))
        {
            return string.Equals(remoteFile.ETag, state.RemoteETag, StringComparison.Ordinal);
        }

        return state.RemoteFileId.HasValue && remoteFile.Id == state.RemoteFileId.Value;
    }

    private static void ValidateOptions(SyncRunOptions options)
    {
        if (options.MaximumLocalDeletesPerRun < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum local deletes per run cannot be negative.");
        }

        if (options.MaximumRemoteDeletesPerRun < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum remote deletes per run cannot be negative.");
        }

        if (options.MaximumStoredResultActivities < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum stored result activities cannot be negative.");
        }
    }

    private static void EnsureEnoughLocalFreeSpace(string localRootPath, string relativePath, long requiredBytes)
    {
        if (requiredBytes <= 0)
        {
            return;
        }

        long? availableFreeBytes = TryGetAvailableFreeBytes(localRootPath);
        if (!availableFreeBytes.HasValue || availableFreeBytes.Value >= requiredBytes)
        {
            return;
        }

        string displayPath = string.IsNullOrWhiteSpace(relativePath) ? "remote file" : relativePath;
        throw new LocalInsufficientDiskSpaceException(displayPath, requiredBytes, availableFreeBytes.Value);
    }

    private static long? TryGetAvailableFreeBytes(string localRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localRootPath);
        try
        {
            string fullRoot = Path.GetFullPath(localRootPath);
            Directory.CreateDirectory(fullRoot);
            string? driveRoot = Path.GetPathRoot(fullRoot);
            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return null;
            }

            var drive = new DriveInfo(driveRoot);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void EnsureEnoughLocalFreeSpaceForPlannedDownloads(
        string localRootPath,
        IReadOnlyList<string> pathKeys,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath)
    {
        long? availableFreeBytes = TryGetAvailableFreeBytes(localRootPath);
        if (!availableFreeBytes.HasValue)
        {
            return;
        }

        long simulatedFreeBytes = availableFreeBytes.Value;
        foreach (string key in pathKeys)
        {
            if (!TryCreatePlannedLocalDownload(
                    key,
                    localByPath,
                    remoteByPath,
                    stateByPath,
                    out string relativePath,
                    out long downloadBytes,
                    out long replacedLocalBytes))
            {
                continue;
            }

            if (downloadBytes <= 0)
            {
                continue;
            }

            if (simulatedFreeBytes < downloadBytes)
            {
                throw new LocalInsufficientDiskSpaceException(relativePath, downloadBytes, simulatedFreeBytes);
            }

            simulatedFreeBytes += replacedLocalBytes - downloadBytes;
        }
    }

    private static long CalculatePlannedTransferBytesTotal(
        IReadOnlyList<string> pathKeys,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath)
    {
        long totalBytes = 0;
        foreach (string key in pathKeys)
        {
            if (TryCalculatePlannedTransferBytes(key, localByPath, remoteByPath, stateByPath, out long transferBytes)
                && transferBytes > 0)
            {
                totalBytes += transferBytes;
            }
        }

        return totalBytes;
    }

    private static long CalculatePlannedTransferBytes(
        string key,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath)
    {
        return TryCalculatePlannedTransferBytes(key, localByPath, remoteByPath, stateByPath, out long transferBytes)
            ? transferBytes
            : 0;
    }

    private static bool TryCalculatePlannedTransferBytes(
        string key,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        out long transferBytes)
    {
        localByPath.TryGetValue(key, out LocalFileSnapshot? local);
        remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
        stateByPath.TryGetValue(key, out SyncStateEntry? state);

        if (state is null)
        {
            return TryCalculateUntrackedTransferBytes(local, remote, out transferBytes);
        }

        if (local is not null && remote is not null && ContentMatches(local.ContentHash, remote.File.ContentHash))
        {
            transferBytes = 0;
            return false;
        }

        bool localDeleted = local is null && !string.IsNullOrWhiteSpace(state.LocalContentHash);
        bool remoteDeleted = remote is null && state.RemoteFileId.HasValue;
        bool localChanged = local is not null && !ContentMatches(local.ContentHash, state.LocalContentHash);
        bool remoteChanged = remote is not null && RemoteMatchesBaseline(remote.File, state) is false;
        bool baselineDiverged = !ContentMatches(state.LocalContentHash, state.RemoteContentHash);

        if (baselineDiverged)
        {
            if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
            {
                transferBytes = 0;
                return false;
            }

            return TryCalculateConflictTransferBytes(local, remote?.File, out transferBytes);
        }

        if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
        {
            transferBytes = 0;
            return false;
        }

        if (localDeleted && remoteChanged)
        {
            return TryCalculateConflictTransferBytes(local, remote?.File, out transferBytes);
        }

        if (remoteDeleted && localChanged && local is not null)
        {
            transferBytes = local.SizeBytes;
            return true;
        }

        if (localChanged && !remoteChanged && local is not null)
        {
            transferBytes = local.SizeBytes;
            return true;
        }

        if (remoteChanged && !localChanged && remote is not null)
        {
            transferBytes = remote.File.SizeBytes;
            return true;
        }

        if ((localChanged && remoteChanged) || (localDeleted && remoteChanged) || (remoteDeleted && localChanged))
        {
            return TryCalculateConflictTransferBytes(local, remote?.File, out transferBytes);
        }

        transferBytes = 0;
        return false;
    }

    private static bool TryCalculateUntrackedTransferBytes(
        LocalFileSnapshot? local,
        RemoteFileSnapshot? remote,
        out long transferBytes)
    {
        if (local is not null && remote is null)
        {
            transferBytes = local.SizeBytes;
            return true;
        }

        if (local is null && remote is not null)
        {
            transferBytes = remote.File.SizeBytes;
            return true;
        }

        if (local is not null
            && remote is not null
            && !string.IsNullOrWhiteSpace(local.ContentHash)
            && !ContentMatches(local.ContentHash, remote.File.ContentHash))
        {
            transferBytes = remote.File.SizeBytes;
            return true;
        }

        transferBytes = 0;
        return false;
    }

    private static bool TryCalculateConflictTransferBytes(
        LocalFileSnapshot? local,
        NodeFileManifestDto? remoteFile,
        out long transferBytes)
    {
        if (local is not null && remoteFile is null)
        {
            transferBytes = local.SizeBytes;
            return true;
        }

        if (remoteFile is not null)
        {
            transferBytes = remoteFile.SizeBytes;
            return true;
        }

        transferBytes = 0;
        return false;
    }

    private static bool TryCreatePlannedLocalDownload(
        string key,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        out string relativePath,
        out long downloadBytes,
        out long replacedLocalBytes)
    {
        localByPath.TryGetValue(key, out LocalFileSnapshot? local);
        remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
        stateByPath.TryGetValue(key, out SyncStateEntry? state);
        relativePath = local?.RelativePath ?? remote?.RelativePath ?? state?.RelativePath ?? key;

        if (state is null)
        {
            return TryCreateRemoteOnlyDownload(local, remote, out downloadBytes, out replacedLocalBytes);
        }

        if (local is not null && remote is not null && ContentMatches(local.ContentHash, remote.File.ContentHash))
        {
            downloadBytes = 0;
            replacedLocalBytes = 0;
            return false;
        }

        bool localDeleted = local is null && !string.IsNullOrWhiteSpace(state.LocalContentHash);
        bool remoteDeleted = remote is null && state.RemoteFileId.HasValue;
        bool localChanged = local is not null && !ContentMatches(local.ContentHash, state.LocalContentHash);
        bool remoteChanged = remote is not null && !RemoteMatchesBaseline(remote.File, state);
        bool baselineDiverged = !ContentMatches(state.LocalContentHash, state.RemoteContentHash);

        if (baselineDiverged)
        {
            if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
            {
                downloadBytes = 0;
                replacedLocalBytes = 0;
                return false;
            }

            return TryCreateConflictDownload(remote, out downloadBytes, out replacedLocalBytes);
        }

        if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
        {
            downloadBytes = 0;
            replacedLocalBytes = 0;
            return false;
        }

        if (localDeleted && remoteChanged)
        {
            return TryCreateConflictDownload(remote, out downloadBytes, out replacedLocalBytes);
        }

        if (remoteChanged && !localChanged && remote is not null)
        {
            downloadBytes = remote.File.SizeBytes;
            replacedLocalBytes = local?.SizeBytes ?? 0;
            return true;
        }

        downloadBytes = 0;
        replacedLocalBytes = 0;
        return false;
    }

    private static bool TryCreateRemoteOnlyDownload(
        LocalFileSnapshot? local,
        RemoteFileSnapshot? remote,
        out long downloadBytes,
        out long replacedLocalBytes)
    {
        if (local is null && remote is not null)
        {
            downloadBytes = remote.File.SizeBytes;
            replacedLocalBytes = 0;
            return true;
        }

        downloadBytes = 0;
        replacedLocalBytes = 0;
        return false;
    }

    private static bool TryCreateConflictDownload(
        RemoteFileSnapshot? remote,
        out long downloadBytes,
        out long replacedLocalBytes)
    {
        if (remote is null)
        {
            downloadBytes = 0;
            replacedLocalBytes = 0;
            return false;
        }

        downloadBytes = remote.File.SizeBytes;
        replacedLocalBytes = 0;
        return true;
    }

    private static SyncDeleteGuard BuildDeleteGuard(
        SyncRunOptions options,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, SyncStateEntry> directoryStateByPath,
        DirectoryContentIndex localDirectoryContentIndex,
        DirectoryContentIndex remoteDirectoryContentIndex)
    {
        if (stateByPath.Count == 0 && directoryStateByPath.Count == 0)
        {
            return new SyncDeleteGuard(options, plannedLocalDeletes: 0, plannedRemoteDeletes: 0);
        }

        int plannedLocalDeletes = 0;
        int plannedRemoteDeletes = 0;

        foreach (KeyValuePair<string, SyncStateEntry> state in stateByPath)
        {
            localByPath.TryGetValue(state.Key, out LocalFileSnapshot? local);
            remoteByPath.TryGetValue(state.Key, out RemoteFileSnapshot? remote);

            switch (GetPlannedDeleteDirection(state.Value, local, remote))
            {
                case SyncDeleteDirection.Local:
                    plannedLocalDeletes++;
                    break;
                case SyncDeleteDirection.Remote:
                    plannedRemoteDeletes++;
                    break;
            }
        }

        foreach (KeyValuePair<string, SyncStateEntry> state in directoryStateByPath)
        {
            localDirectoriesByPath.TryGetValue(state.Key, out LocalDirectorySnapshot? local);
            remoteDirectoriesByPath.TryGetValue(state.Key, out RemoteDirectorySnapshot? remote);

            switch (GetPlannedDirectoryDeleteDirection(
                state.Value,
                local,
                remote,
                localDirectoryContentIndex,
                remoteDirectoryContentIndex))
            {
                case SyncDeleteDirection.Local:
                    plannedLocalDeletes++;
                    break;
                case SyncDeleteDirection.Remote:
                    plannedRemoteDeletes++;
                    break;
            }
        }

        return new SyncDeleteGuard(options, plannedLocalDeletes, plannedRemoteDeletes);
    }

    private static bool HasLocalDirectoryDeleteCandidates(
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, SyncStateEntry> directoryStateByPath)
    {
        foreach (KeyValuePair<string, SyncStateEntry> state in directoryStateByPath)
        {
            if (state.Value.RemoteNodeId is null)
            {
                continue;
            }

            if (localDirectoriesByPath.ContainsKey(state.Key) && !remoteDirectoriesByPath.ContainsKey(state.Key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRemoteDirectoryDeleteCandidates(
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, SyncStateEntry> directoryStateByPath)
    {
        foreach (KeyValuePair<string, SyncStateEntry> state in directoryStateByPath)
        {
            if (state.Value.RemoteNodeId is null)
            {
                continue;
            }

            if (!localDirectoriesByPath.ContainsKey(state.Key) && remoteDirectoriesByPath.ContainsKey(state.Key))
            {
                return true;
            }
        }

        return false;
    }

    private static SyncDeleteDirection GetPlannedDirectoryDeleteDirection(
        SyncStateEntry state,
        LocalDirectorySnapshot? local,
        RemoteDirectorySnapshot? remote,
        DirectoryContentIndex localDirectoryContentIndex,
        DirectoryContentIndex remoteDirectoryContentIndex)
    {
        if (state.RemoteNodeId is null || local is null && remote is null)
        {
            return SyncDeleteDirection.None;
        }

        string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state.RelativePath;
        if (local is null && remote is not null && !remoteDirectoryContentIndex.HasChildren(relativePath))
        {
            return SyncDeleteDirection.Remote;
        }

        if (remote is null && local is not null && !localDirectoryContentIndex.HasChildren(relativePath))
        {
            return SyncDeleteDirection.Local;
        }

        return SyncDeleteDirection.None;
    }

    private static SyncDeleteDirection GetPlannedDeleteDirection(
        SyncStateEntry? state,
        LocalFileSnapshot? local,
        RemoteFileSnapshot? remote)
    {
        if (state is null)
        {
            return SyncDeleteDirection.None;
        }

        bool localDeleted = local is null && !string.IsNullOrWhiteSpace(state.LocalContentHash);
        bool remoteDeleted = remote is null && state.RemoteFileId.HasValue;
        bool localChanged = local is not null && !ContentMatches(local.ContentHash, state.LocalContentHash);
        bool remoteChanged = remote is not null && !RemoteMatchesBaseline(remote.File, state);
        bool baselineDiverged = !ContentMatches(state.LocalContentHash, state.RemoteContentHash);

        if (local is null && remote is null)
        {
            return SyncDeleteDirection.None;
        }

        if (local is not null && remote is not null && ContentMatches(local.ContentHash, remote.File.ContentHash))
        {
            return SyncDeleteDirection.None;
        }

        if (baselineDiverged)
        {
            return SyncDeleteDirection.None;
        }

        if (!localDeleted && !remoteDeleted && !localChanged && !remoteChanged)
        {
            return SyncDeleteDirection.None;
        }

        if (localDeleted && remoteDeleted)
        {
            return SyncDeleteDirection.None;
        }

        if (localDeleted && !remoteChanged && remote is not null)
        {
            return SyncDeleteDirection.Remote;
        }

        if (remoteDeleted && !localChanged && local is not null)
        {
            return SyncDeleteDirection.Local;
        }

        return SyncDeleteDirection.None;
    }

    private static bool ContentMatches(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemotePreconditionFailed(HttpRequestException exception)
    {
        return exception.StatusCode == HttpStatusCode.PreconditionFailed;
    }

    private static Dictionary<string, T> ToDictionary<T>(IEnumerable<T> entries, Func<T, string> pathSelector)
    {
        var result = new Dictionary<string, T>(PathComparer);
        foreach (T entry in entries)
        {
            string relativePath = pathSelector(entry);
            if (SyncPathIgnoreRules.ShouldIgnore(relativePath))
            {
                continue;
            }

            string key = SyncPath.ToKey(relativePath);
            if (result.TryGetValue(key, out T? existing))
            {
                throw new SyncPathCollisionException(pathSelector(existing), relativePath);
            }

            result[key] = entry;
        }

        return result;
    }

    private static void ThrowIfPathKindCollisions<TLeft, TRight>(
        IReadOnlyDictionary<string, TLeft> left,
        IReadOnlyDictionary<string, TRight> right,
        Func<TLeft, string> leftPathSelector,
        Func<TRight, string> rightPathSelector)
    {
        foreach (KeyValuePair<string, TLeft> item in left)
        {
            if (right.TryGetValue(item.Key, out TRight? colliding))
            {
                throw new SyncPathCollisionException(leftPathSelector(item.Value), rightPathSelector(colliding));
            }
        }
    }

    private static IReadOnlyList<string> BuildPathKeys(params IEnumerable<string>[] keySets)
    {
        List<string> keys = BuildUniquePathKeyList(keySets);
        keys.Sort(PathComparer.Compare);
        return keys;
    }

    private static IReadOnlyList<string> BuildDirectoryPathKeys(params IEnumerable<string>[] keySets)
    {
        List<string> keys = BuildUniquePathKeyList(keySets);
        keys.Sort(static (left, right) =>
        {
            int depthComparison = GetPathDepth(left).CompareTo(GetPathDepth(right));
            return depthComparison != 0
                ? depthComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left, right);
        });
        return keys;
    }

    private static List<string> BuildUniquePathKeyList(params IEnumerable<string>[] keySets)
    {
        if (TryBuildSingleSourcePathKeyList(keySets, out List<string> singleSourceKeys))
        {
            return singleSourceKeys;
        }

        int initialCapacity = EstimateUniquePathKeyCapacity(keySets);
        var seen = new HashSet<string>(initialCapacity, PathComparer);
        var keys = new List<string>(initialCapacity);
        foreach (IEnumerable<string> keySet in keySets)
        {
            foreach (string key in keySet)
            {
                if (seen.Add(key))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    private static bool TryBuildSingleSourcePathKeyList(IEnumerable<string>[] keySets, out List<string> keys)
    {
        IEnumerable<string>? singleSource = null;
        int singleSourceCount = 0;
        foreach (IEnumerable<string> keySet in keySets)
        {
            if (!keySet.TryGetNonEnumeratedCount(out int count))
            {
                keys = [];
                return false;
            }

            if (count == 0)
            {
                continue;
            }

            if (singleSource is not null)
            {
                keys = [];
                return false;
            }

            singleSource = keySet;
            singleSourceCount = count;
        }

        keys = singleSource is null ? [] : new List<string>(singleSourceCount);
        if (singleSource is not null)
        {
            keys.AddRange(singleSource);
        }

        return true;
    }

    private static int EstimateUniquePathKeyCapacity(IEnumerable<string>[] keySets)
    {
        int capacity = 0;
        foreach (IEnumerable<string> keySet in keySets)
        {
            if (keySet.TryGetNonEnumeratedCount(out int count) && count > capacity)
            {
                capacity = count;
            }
        }

        return capacity;
    }

    private static IEnumerable<string> EnumerateDirectoryDeleteKeys(IReadOnlyList<string> pathKeys)
    {
        for (int index = pathKeys.Count - 1; index >= 0;)
        {
            int depth = GetPathDepth(pathKeys[index]);
            int groupStart = index;
            while (groupStart > 0 && GetPathDepth(pathKeys[groupStart - 1]) == depth)
            {
                groupStart--;
            }

            for (int groupIndex = groupStart; groupIndex <= index; groupIndex++)
            {
                yield return pathKeys[groupIndex];
            }

            index = groupStart - 1;
        }
    }

    private static void Report(
        SyncRunResult result,
        SyncRunOptions options,
        SyncActivityKind kind,
        string relativePath,
        string? details,
        bool requiresUserAction = false)
    {
        var activity = new SyncActivity
        {
            Kind = kind,
            RelativePath = SyncPath.Normalize(relativePath),
            Details = details,
            RequiresUserAction = requiresUserAction,
        };
        result.RecordActivity(activity, options.MaximumStoredResultActivities);
        options.ActivityProgress?.Report(activity);
    }

    private static void ReportTransfer(
        SyncRunOptions options,
        SyncTransferDirection direction,
        string relativePath,
        long transferredBytes,
        long? totalBytes,
        bool isCompleted = false)
    {
        options.TransferProgress?.Report(new SyncTransferProgress(
            direction,
            relativePath,
            transferredBytes,
            totalBytes,
            isCompleted));
    }

    private async Task EnsureLocalContentHashAsync(LocalFileSnapshot local, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(local.ContentHash))
        {
            return;
        }

        if (_localContentHasher is null)
        {
            throw new InvalidOperationException("Local file snapshot does not include a content hash and no local content hasher is available.");
        }

        local.ContentHash = await _localContentHasher.ComputeContentHashAsync(local, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureLocalContentHashForBaselineComparisonAsync(
        LocalFileSnapshot local,
        SyncStateEntry state,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(local.ContentHash))
        {
            return;
        }

        if (CanReuseBaselineLocalContentHash(local, state))
        {
            local.ContentHash = state.LocalContentHash!;
            return;
        }

        await EnsureLocalContentHashAsync(local, cancellationToken).ConfigureAwait(false);
    }

    private static bool CanReuseBaselineLocalContentHash(LocalFileSnapshot local, SyncStateEntry state)
    {
        return !string.IsNullOrWhiteSpace(state.LocalContentHash)
            && state.LocalSizeBytes.HasValue
            && state.LocalSizeBytes.Value == local.SizeBytes
            && state.LocalLastWriteUtc.HasValue
            && state.LocalLastWriteUtc.Value.ToUniversalTime() == local.LastWriteUtc.ToUniversalTime();
    }

    private static string ResolveUploadedLocalContentHash(LocalFileSnapshot local, NodeFileManifestDto uploaded)
    {
        if (!string.IsNullOrWhiteSpace(local.ContentHash))
        {
            return local.ContentHash;
        }

        if (!string.IsNullOrWhiteSpace(uploaded.ContentHash))
        {
            return uploaded.ContentHash;
        }

        throw new InvalidOperationException("Uploaded file manifest does not include a content hash.");
    }

    private static void ReportRunProgress(
        SyncRunOptions options,
        SyncRunProgressStage stage,
        int filesCompleted,
        int? filesTotal,
        string? currentPath,
        DateTime startedAtUtc,
        bool isCompleted = false,
        long bytesCompleted = 0,
        long? bytesTotal = null)
    {
        options.RunProgress?.Report(new SyncRunProgress(
            stage,
            filesCompleted,
            filesTotal,
            currentPath,
            startedAtUtc,
            isCompleted,
            bytesCompleted,
            bytesTotal));
    }

    private static void ReportItemRunProgress(
        SyncRunOptions options,
        SyncRunProgressStage stage,
        int itemsCompleted,
        int itemsTotal,
        string? currentPath,
        DateTime startedAtUtc,
        ref DateTime? lastReportedAtUtc,
        long bytesCompleted = 0,
        long? bytesTotal = null)
    {
        DateTime occurredAtUtc = DateTime.UtcNow;
        if (!ShouldReportItemRunProgress(itemsCompleted, itemsTotal, lastReportedAtUtc, occurredAtUtc))
        {
            return;
        }

        lastReportedAtUtc = occurredAtUtc;

        ReportRunProgress(
            options,
            stage,
            itemsCompleted,
            itemsTotal,
            currentPath,
            startedAtUtc,
            bytesCompleted: bytesCompleted,
            bytesTotal: bytesTotal);
    }

    private static bool ShouldReportItemRunProgress(
        int itemsCompleted,
        int itemsTotal,
        DateTime? lastReportedAtUtc,
        DateTime occurredAtUtc)
    {
        return itemsTotal <= RunProgressReportItemInterval
            || itemsCompleted == 0
            || itemsCompleted == itemsTotal
            || itemsCompleted % RunProgressReportItemInterval == 0
            || (lastReportedAtUtc.HasValue
                && occurredAtUtc - lastReportedAtUtc.Value >= RunProgressReportTimeInterval);
    }

    private enum SyncDeleteDirection
    {
        None,
        Local,
        Remote,
    }

    private sealed class DirectoryContentIndex
    {
        private readonly HashSet<string> _directoryKeysWithChildren;

        private DirectoryContentIndex(HashSet<string> directoryKeysWithChildren)
        {
            _directoryKeysWithChildren = directoryKeysWithChildren;
        }

        public static DirectoryContentIndex Empty { get; } = new([]);

        public static DirectoryContentIndex Create(IEnumerable<string> directoryKeys, IEnumerable<string> fileKeys)
        {
            var directoryKeysWithChildren = new HashSet<string>(PathComparer);
            AddAncestorDirectoryKeys(directoryKeysWithChildren, directoryKeys);
            AddAncestorDirectoryKeys(directoryKeysWithChildren, fileKeys);
            return new DirectoryContentIndex(directoryKeysWithChildren);
        }

        public bool HasChildren(string relativePath)
        {
            return _directoryKeysWithChildren.Contains(SyncPath.ToKey(relativePath));
        }

        private static void AddAncestorDirectoryKeys(HashSet<string> directoryKeysWithChildren, IEnumerable<string> childKeys)
        {
            foreach (string childKey in childKeys)
            {
                AddAncestorDirectoryKeys(directoryKeysWithChildren, childKey);
            }
        }

        private static void AddAncestorDirectoryKeys(HashSet<string> directoryKeysWithChildren, string childKey)
        {
            string currentKey = childKey;
            while (!string.IsNullOrEmpty(currentKey))
            {
                int separatorIndex = currentKey.LastIndexOf('/');
                if (separatorIndex < 0)
                {
                    break;
                }

                string parentKey = currentKey[..separatorIndex];
                directoryKeysWithChildren.Add(parentKey);
                currentKey = parentKey;
            }
        }
    }

    private sealed class LocalTreeScanProgressReporter : IProgress<LocalTreeScanProgress>
    {
        private readonly SyncRunOptions _options;
        private readonly DateTime _startedAtUtc;

        public LocalTreeScanProgressReporter(SyncRunOptions options, DateTime startedAtUtc)
        {
            _options = options;
            _startedAtUtc = startedAtUtc;
        }

        public void Report(LocalTreeScanProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            ReportRunProgress(
                _options,
                SyncRunProgressStage.ScanningLocal,
                value.FilesScanned,
                filesTotal: null,
                value.CurrentPath,
                _startedAtUtc);
        }
    }

    private sealed class RemoteTreeScanProgressReporter : IProgress<RemoteTreeScanProgress>
    {
        private readonly SyncRunOptions _options;
        private readonly DateTime _startedAtUtc;

        public RemoteTreeScanProgressReporter(SyncRunOptions options, DateTime startedAtUtc)
        {
            _options = options;
            _startedAtUtc = startedAtUtc;
        }

        public void Report(RemoteTreeScanProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            ReportRunProgress(
                _options,
                SyncRunProgressStage.ScanningRemote,
                value.FilesScanned,
                filesTotal: null,
                value.CurrentPath,
                _startedAtUtc);
        }
    }

    private sealed class SyncTreeLookups
    {
        public SyncTreeLookups(
            Dictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
            Dictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
            Dictionary<string, LocalFileSnapshot> localFilesByPath,
            Dictionary<string, RemoteFileSnapshot> remoteFilesByPath,
            NodeDto remoteRootNode)
        {
            LocalDirectoriesByPath = localDirectoriesByPath;
            RemoteDirectoriesByPath = remoteDirectoriesByPath;
            LocalFilesByPath = localFilesByPath;
            RemoteFilesByPath = remoteFilesByPath;
            RemoteRootNode = remoteRootNode;
        }

        public Dictionary<string, LocalDirectorySnapshot> LocalDirectoriesByPath { get; }

        public Dictionary<string, RemoteDirectorySnapshot> RemoteDirectoriesByPath { get; }

        public Dictionary<string, LocalFileSnapshot> LocalFilesByPath { get; }

        public Dictionary<string, RemoteFileSnapshot> RemoteFilesByPath { get; }

        public NodeDto RemoteRootNode { get; }
    }

    private sealed class SyncDeleteGuard
    {
        private readonly int _maximumLocalDeletes;
        private readonly int _maximumRemoteDeletes;
        private readonly int _plannedLocalDeletes;
        private readonly int _plannedRemoteDeletes;

        public SyncDeleteGuard(SyncRunOptions options, int plannedLocalDeletes, int plannedRemoteDeletes)
        {
            _maximumLocalDeletes = options.MaximumLocalDeletesPerRun;
            _maximumRemoteDeletes = options.MaximumRemoteDeletesPerRun;
            _plannedLocalDeletes = plannedLocalDeletes;
            _plannedRemoteDeletes = plannedRemoteDeletes;
        }

        public bool CanDeleteLocal(out string? details)
        {
            return CanDelete(
                _plannedLocalDeletes,
                _maximumLocalDeletes,
                "Local delete blocked by mass-delete guard.",
                out details);
        }

        public bool CanDeleteRemote(out string? details)
        {
            return CanDelete(
                _plannedRemoteDeletes,
                _maximumRemoteDeletes,
                "Remote delete blocked by mass-delete guard.",
                out details);
        }

        private static bool CanDelete(
            int planned,
            int maximum,
            string blockedDetails,
            out string? details)
        {
            if (planned > maximum)
            {
                details = blockedDetails + " " + planned + " pending deletes exceed limit " + maximum + ".";
                return false;
            }

            details = null;
            return true;
        }
    }
}
