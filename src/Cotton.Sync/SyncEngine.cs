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
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly ILocalFileScanner _localScanner;
    private readonly ILocalFileContentHasher? _localContentHasher;
    private readonly ILocalFileMetadataTreeScanner? _localMetadataTreeScanner;
    private readonly ILocalTreeScanner? _localTreeScanner;
    private readonly IRemoteDirectorySynchronizer? _remoteDirectories;
    private readonly IRemoteTreeCrawler _remoteCrawler;
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
        _localTreeScanner = localScanner as ILocalTreeScanner;
        _remoteCrawler = remoteCrawler ?? throw new ArgumentNullException(nameof(remoteCrawler));
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
        LocalTreeSnapshot localTree = await ScanLocalTreeAsync(syncPair.LocalRootPath, cancellationToken).ConfigureAwait(false);
        ReportRunProgress(options, SyncRunProgressStage.ScanningRemote, 0, null, null, startedAtUtc);
        RemoteTreeSnapshot remoteTree = await _remoteCrawler.CrawlAsync(syncPair.RemoteRootNodeId, cancellationToken).ConfigureAwait(false);
        List<SyncStateEntry> stateEntries = (await _stateStore
            .LoadPairAsync(syncPair.SyncPairId, cancellationToken)
            .ConfigureAwait(false)).ToList();
        await RemoveIgnoredStateEntriesAsync(syncPair.SyncPairId, stateEntries, cancellationToken).ConfigureAwait(false);
        var result = new SyncRunResult();

        Dictionary<string, LocalDirectorySnapshot> localDirectoriesByPath = ToDictionary(localTree.Directories, directory => directory.RelativePath);
        Dictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath = ToDictionary(remoteTree.Directories, directory => directory.RelativePath);
        Dictionary<string, SyncStateEntry> directoryStateByPath = ToDictionary(
            stateEntries.Where(entry => entry.Kind == SyncEntryKind.Directory),
            entry => entry.RelativePath);
        Dictionary<string, LocalFileSnapshot> localByPath = ToDictionary(localTree.Files, file => file.RelativePath);
        Dictionary<string, RemoteFileSnapshot> remoteByPath = ToDictionary(remoteTree.Files, file => file.RelativePath);
        Dictionary<string, SyncStateEntry> stateByPath = ToDictionary(
            stateEntries.Where(entry => entry.Kind == SyncEntryKind.File),
            entry => entry.RelativePath);
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
        List<string> pathKeys = BuildPathKeys(localByPath.Keys, remoteByPath.Keys, stateByPath.Keys);
        ReportRunProgress(options, SyncRunProgressStage.ReconcilingDirectories, 0, pathKeys.Count, null, startedAtUtc);
        await ReconcileDirectoriesWithoutBaselineAsync(
            syncPair,
            options,
            result,
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath,
            remoteTree.RootNode,
            cancellationToken).ConfigureAwait(false);

        await EnsureLocalContentHashesForStateFilesAsync(pathKeys, localByPath, stateByPath, cancellationToken)
            .ConfigureAwait(false);

        SyncDeleteGuard deleteGuard = BuildDeleteGuard(
            options,
            pathKeys,
            localByPath,
            remoteByPath,
            stateByPath,
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath);

        await ReconcileDirectoryDeletesAsync(
            syncPair,
            options,
            result,
            deleteGuard,
            localDirectoriesByPath,
            remoteDirectoriesByPath,
            directoryStateByPath,
            localByPath,
            remoteByPath,
            cancellationToken).ConfigureAwait(false);

        int filesCompleted = 0;
        ReportRunProgress(options, SyncRunProgressStage.ReconcilingFiles, filesCompleted, pathKeys.Count, null, startedAtUtc);
        foreach (string key in pathKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            localByPath.TryGetValue(key, out LocalFileSnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
            stateByPath.TryGetValue(key, out SyncStateEntry? state);
            string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state?.RelativePath ?? key;
            ReportRunProgress(options, SyncRunProgressStage.ReconcilingFiles, filesCompleted, pathKeys.Count, relativePath, startedAtUtc);

            if (state is null)
            {
                await ReconcileWithoutBaselineAsync(syncPair, options, result, relativePath, local, remote, cancellationToken).ConfigureAwait(false);
                filesCompleted++;
                ReportRunProgress(options, SyncRunProgressStage.ReconcilingFiles, filesCompleted, pathKeys.Count, relativePath, startedAtUtc);
                continue;
            }

            await ReconcileWithBaselineAsync(syncPair, options, result, deleteGuard, state, relativePath, local, remote, cancellationToken)
                .ConfigureAwait(false);
            filesCompleted++;
            ReportRunProgress(options, SyncRunProgressStage.ReconcilingFiles, filesCompleted, pathKeys.Count, relativePath, startedAtUtc);
        }

        ReportRunProgress(options, SyncRunProgressStage.Completed, filesCompleted, pathKeys.Count, null, startedAtUtc, isCompleted: true);
        _logger.LogInformation(
            "Completed sync pass for pair {SyncPairId} with {ActivityCount} activities.",
            syncPair.SyncPairId,
            result.Activities.Count);
        return result;
    }

    private async Task RemoveIgnoredStateEntriesAsync(
        string syncPairId,
        List<SyncStateEntry> stateEntries,
        CancellationToken cancellationToken)
    {
        for (int index = stateEntries.Count - 1; index >= 0; index--)
        {
            SyncStateEntry entry = stateEntries[index];
            if (!SyncPathIgnoreRules.ShouldIgnore(entry.RelativePath))
            {
                continue;
            }

            await _stateStore.DeleteAsync(syncPairId, entry.RelativePath, cancellationToken).ConfigureAwait(false);
            stateEntries.RemoveAt(index);
        }
    }

    private async Task EnsureLocalContentHashesForStateFilesAsync(
        IReadOnlyList<string> pathKeys,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        CancellationToken cancellationToken)
    {
        foreach (string key in pathKeys)
        {
            if (stateByPath.ContainsKey(key) && localByPath.TryGetValue(key, out LocalFileSnapshot? local))
            {
                await EnsureLocalContentHashAsync(local, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<LocalTreeSnapshot> ScanLocalTreeAsync(string localRootPath, CancellationToken cancellationToken)
    {
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

    private async Task ReconcileDirectoriesWithoutBaselineAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localByPath,
        IDictionary<string, RemoteDirectorySnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        NodeDto remoteRootNode,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> remoteNodeIdsByPath = remoteByPath.ToDictionary(
            static item => item.Key,
            static item => item.Value.Node.Id,
            PathComparer);
        remoteNodeIdsByPath[string.Empty] = remoteRootNode.Id;

        List<string> pathKeys = BuildPathKeys(localByPath.Keys, remoteByPath.Keys, stateByPath.Keys)
            .OrderBy(static key => GetPathDepth(key))
            .ThenBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (string key in pathKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stateByPath.ContainsKey(key))
            {
                continue;
            }

            localByPath.TryGetValue(key, out LocalDirectorySnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteDirectorySnapshot? remote);
            string relativePath = local?.RelativePath ?? remote?.RelativePath ?? key;
            if (local is null && remote is not null)
            {
                await _localWriter.CreateDirectoryAsync(syncPair.LocalRootPath, relativePath, cancellationToken).ConfigureAwait(false);
                await _stateStore.UpsertAsync(BuildDirectoryBaseline(syncPair, relativePath, remote.Node), cancellationToken)
                    .ConfigureAwait(false);
                Report(result, options, SyncActivityKind.Downloaded, relativePath, "Created local folder.");
                continue;
            }

            if (local is not null && remote is null && _remoteDirectories is not null)
            {
                string parentPath = GetParentPath(relativePath);
                string parentKey = string.IsNullOrEmpty(parentPath) ? string.Empty : SyncPath.ToKey(parentPath);
                if (!remoteNodeIdsByPath.TryGetValue(parentKey, out Guid parentNodeId))
                {
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
                remoteNodeIdsByPath[SyncPath.ToKey(relativePath)] = created.Id;
                await _stateStore.UpsertAsync(BuildDirectoryBaseline(syncPair, relativePath, created), cancellationToken)
                    .ConfigureAwait(false);
                Report(result, options, SyncActivityKind.Uploaded, relativePath, "Created remote folder.");
                continue;
            }

            if (local is not null && remote is not null)
            {
                await _stateStore.UpsertAsync(BuildDirectoryBaseline(syncPair, relativePath, remote.Node), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task ReconcileDirectoryDeletesAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteGuard deleteGuard,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        IReadOnlyDictionary<string, LocalFileSnapshot> localFilesByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteFilesByPath,
        CancellationToken cancellationToken)
    {
        List<string> stateKeys = stateByPath.Keys
            .OrderByDescending(static key => GetPathDepth(key))
            .ThenBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (string key in stateKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyncStateEntry state = stateByPath[key];
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
                    remoteByPath,
                    remoteFilesByPath,
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
                    localByPath,
                    localFilesByPath,
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
                await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, remote.File), cancellationToken)
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
            await EnsureLocalContentHashAsync(local, cancellationToken).ConfigureAwait(false);
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
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, remote.File), cancellationToken)
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
        await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, localContentHash, local.LastWriteUtc, uploaded), cancellationToken)
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
        await _localWriter.WriteFileAsync(
            syncPair.LocalRootPath,
            relativePath,
            (stream, token) => DownloadAndVerifyFileAsync(remoteFile, relativePath, options, stream, token),
            remoteFile.UpdatedAt == default ? null : remoteFile.UpdatedAt,
            cancellationToken).ConfigureAwait(false);
        await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, remoteFile.ContentHash, remoteFile.UpdatedAt, remoteFile), cancellationToken)
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
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteFilesByPath,
        CancellationToken cancellationToken)
    {
        if (_remoteDirectories is null)
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, "Remote folder delete is not available.");
            return;
        }

        if (!IsRemoteDirectoryEmpty(relativePath, remoteDirectoriesByPath, remoteFilesByPath))
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
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, LocalFileSnapshot> localFilesByPath,
        CancellationToken cancellationToken)
    {
        if (!IsLocalDirectoryEmpty(relativePath, localDirectoriesByPath, localFilesByPath))
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
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, remoteFile), cancellationToken)
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
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, localContentHash, local.LastWriteUtc, uploaded), cancellationToken)
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
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, remoteFile.ContentHash, remoteFile.UpdatedAt, remoteFile), cancellationToken)
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
        NodeFileManifestDto? remoteFile)
    {
        return new SyncStateEntry
        {
            SyncPairId = syncPair.SyncPairId,
            RelativePath = SyncPath.Normalize(relativePath),
            Kind = SyncEntryKind.File,
            LocalContentHash = localContentHash,
            LocalLastWriteUtc = localLastWriteUtc?.ToUniversalTime(),
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

    private static bool IsLocalDirectoryEmpty(
        string relativePath,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, LocalFileSnapshot> localFilesByPath)
    {
        string prefix = SyncPath.ToKey(relativePath) + "/";
        return !HasDescendant(prefix, localDirectoriesByPath.Keys)
            && !HasDescendant(prefix, localFilesByPath.Keys);
    }

    private static bool IsRemoteDirectoryEmpty(
        string relativePath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteFilesByPath)
    {
        string prefix = SyncPath.ToKey(relativePath) + "/";
        return !HasDescendant(prefix, remoteDirectoriesByPath.Keys)
            && !HasDescendant(prefix, remoteFilesByPath.Keys);
    }

    private static bool HasDescendant(string prefix, IEnumerable<string> keys)
    {
        return keys.Any(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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
    }

    private static SyncDeleteGuard BuildDeleteGuard(
        SyncRunOptions options,
        IEnumerable<string> pathKeys,
        IReadOnlyDictionary<string, LocalFileSnapshot> localByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteByPath,
        IReadOnlyDictionary<string, SyncStateEntry> stateByPath,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, SyncStateEntry> directoryStateByPath)
    {
        int plannedLocalDeletes = 0;
        int plannedRemoteDeletes = 0;

        foreach (string key in pathKeys)
        {
            localByPath.TryGetValue(key, out LocalFileSnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
            stateByPath.TryGetValue(key, out SyncStateEntry? state);

            switch (GetPlannedDeleteDirection(state, local, remote))
            {
                case SyncDeleteDirection.Local:
                    plannedLocalDeletes++;
                    break;
                case SyncDeleteDirection.Remote:
                    plannedRemoteDeletes++;
                    break;
            }
        }

        foreach (string key in directoryStateByPath.Keys)
        {
            localDirectoriesByPath.TryGetValue(key, out LocalDirectorySnapshot? local);
            remoteDirectoriesByPath.TryGetValue(key, out RemoteDirectorySnapshot? remote);
            SyncStateEntry state = directoryStateByPath[key];

            switch (GetPlannedDirectoryDeleteDirection(
                state,
                local,
                remote,
                localDirectoriesByPath,
                localByPath,
                remoteDirectoriesByPath,
                remoteByPath))
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

    private static SyncDeleteDirection GetPlannedDirectoryDeleteDirection(
        SyncStateEntry state,
        LocalDirectorySnapshot? local,
        RemoteDirectorySnapshot? remote,
        IReadOnlyDictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
        IReadOnlyDictionary<string, LocalFileSnapshot> localFilesByPath,
        IReadOnlyDictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
        IReadOnlyDictionary<string, RemoteFileSnapshot> remoteFilesByPath)
    {
        if (state.RemoteNodeId is null || local is null && remote is null)
        {
            return SyncDeleteDirection.None;
        }

        string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state.RelativePath;
        if (local is null && remote is not null && IsRemoteDirectoryEmpty(relativePath, remoteDirectoriesByPath, remoteFilesByPath))
        {
            return SyncDeleteDirection.Remote;
        }

        if (remote is null && local is not null && IsLocalDirectoryEmpty(relativePath, localDirectoriesByPath, localFilesByPath))
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

    private static List<string> BuildPathKeys(params IEnumerable<string>[] keySets)
    {
        var keys = new HashSet<string>(PathComparer);
        foreach (IEnumerable<string> keySet in keySets)
        {
            foreach (string key in keySet)
            {
                keys.Add(key);
            }
        }

        return keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToList();
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
        result.Activities.Add(activity);
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
        bool isCompleted = false)
    {
        options.RunProgress?.Report(new SyncRunProgress(
            stage,
            filesCompleted,
            filesTotal,
            currentPath,
            startedAtUtc,
            isCompleted));
    }

    private enum SyncDeleteDirection
    {
        None,
        Local,
        Remote,
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
