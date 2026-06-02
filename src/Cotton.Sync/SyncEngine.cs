// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Files;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync;

/// <summary>
/// Reconciles local and remote file snapshots for one synchronization pair.
/// </summary>
public sealed class SyncEngine : ISyncEngine
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly ILocalFileScanner _localScanner;
    private readonly IRemoteTreeCrawler _remoteCrawler;
    private readonly IRemoteFileSynchronizer _remoteFiles;
    private readonly ISyncStateStore _stateStore;
    private readonly ILocalFileSyncWriter _localWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEngine" /> class.
    /// </summary>
    public SyncEngine(
        ILocalFileScanner localScanner,
        IRemoteTreeCrawler remoteCrawler,
        IRemoteFileSynchronizer remoteFiles,
        ISyncStateStore stateStore,
        ILocalFileSyncWriter? localWriter = null)
    {
        _localScanner = localScanner ?? throw new ArgumentNullException(nameof(localScanner));
        _remoteCrawler = remoteCrawler ?? throw new ArgumentNullException(nameof(remoteCrawler));
        _remoteFiles = remoteFiles ?? throw new ArgumentNullException(nameof(remoteFiles));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _localWriter = localWriter ?? new AtomicLocalFileSyncWriter();
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
        await _stateStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LocalFileSnapshot> localFiles = await _localScanner.ScanAsync(syncPair.LocalRootPath, cancellationToken).ConfigureAwait(false);
        RemoteTreeSnapshot remoteTree = await _remoteCrawler.CrawlAsync(syncPair.RemoteRootNodeId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SyncStateEntry> stateEntries = await _stateStore.LoadPairAsync(syncPair.SyncPairId, cancellationToken).ConfigureAwait(false);

        Dictionary<string, LocalFileSnapshot> localByPath = ToDictionary(localFiles, file => file.RelativePath);
        Dictionary<string, RemoteFileSnapshot> remoteByPath = ToDictionary(remoteTree.Files, file => file.RelativePath);
        Dictionary<string, SyncStateEntry> stateByPath = ToDictionary(
            stateEntries.Where(entry => entry.Kind == SyncEntryKind.File),
            entry => entry.RelativePath);
        List<string> pathKeys = BuildPathKeys(localByPath.Keys, remoteByPath.Keys, stateByPath.Keys);
        var result = new SyncRunResult();
        if (!options.DryRun)
        {
            ValidateDeleteSafety(options, pathKeys, localByPath, remoteByPath, stateByPath);
        }

        foreach (string key in pathKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            localByPath.TryGetValue(key, out LocalFileSnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
            stateByPath.TryGetValue(key, out SyncStateEntry? state);
            string relativePath = local?.RelativePath ?? remote?.RelativePath ?? state?.RelativePath ?? key;

            if (state is null)
            {
                await ReconcileWithoutBaselineAsync(syncPair, options, result, relativePath, local, remote, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await ReconcileWithBaselineAsync(syncPair, options, result, state, relativePath, local, remote, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
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
            if (ContentMatches(local.ContentHash, remote.File.ContentHash))
            {
                if (!options.DryRun)
                {
                    await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, remote.File), cancellationToken)
                        .ConfigureAwait(false);
                }

                return;
            }

            await PreserveConflictAsync(syncPair, options, result, relativePath, local, remote.File, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileWithBaselineAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncStateEntry state,
        string relativePath,
        LocalFileSnapshot? local,
        RemoteFileSnapshot? remote,
        CancellationToken cancellationToken)
    {
        bool localDeleted = local is null && !string.IsNullOrWhiteSpace(state.LocalContentHash);
        bool remoteDeleted = remote is null && state.RemoteFileId.HasValue;
        bool localChanged = local is not null && !ContentMatches(local.ContentHash, state.LocalContentHash);
        bool remoteChanged = remote is not null && !RemoteMatchesBaseline(remote.File, state);
        bool baselineDiverged = !ContentMatches(state.LocalContentHash, state.RemoteContentHash);

        if (local is null && remote is null)
        {
            if (!options.DryRun)
            {
                await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (local is not null && remote is not null && ContentMatches(local.ContentHash, remote.File.ContentHash))
        {
            if (!options.DryRun)
            {
                await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, remote.File), cancellationToken)
                    .ConfigureAwait(false);
            }

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
            await DeleteRemoteAsync(syncPair, options, result, relativePath, remote.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (remoteDeleted && !localChanged && local is not null)
        {
            await DeleteLocalAsync(syncPair, options, result, relativePath, cancellationToken).ConfigureAwait(false);
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
        if (options.DryRun)
        {
            Report(result, options, SyncActivityKind.Uploaded, relativePath, "Dry run: would upload.");
            return;
        }

        NodeFileManifestDto uploaded = await _remoteFiles.UploadFileAsync(
            syncPair.RemoteRootNodeId,
            relativePath,
            local,
            existingRemoteFile,
            cancellationToken).ConfigureAwait(false);
        await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, uploaded), cancellationToken)
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
        if (options.DryRun)
        {
            Report(result, options, SyncActivityKind.Downloaded, relativePath, "Dry run: would download.");
            return;
        }

        await _localWriter.WriteFileAsync(
            syncPair.LocalRootPath,
            relativePath,
            (stream, token) => _remoteFiles.DownloadFileAsync(remoteFile.Id, stream, token),
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
        string relativePath,
        NodeFileManifestDto remoteFile,
        CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            Report(result, options, SyncActivityKind.DeletedRemote, relativePath, "Dry run: would delete remote.");
            return;
        }

        await _remoteFiles.DeleteFileAsync(remoteFile.Id, options.DeleteRemotePermanently, cancellationToken).ConfigureAwait(false);
        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedRemote, relativePath, null);
    }

    private async Task DeleteLocalAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            Report(result, options, SyncActivityKind.DeletedLocal, relativePath, "Dry run: would delete local.");
            return;
        }

        await _localWriter.DeleteFileAsync(syncPair.LocalRootPath, relativePath, cancellationToken).ConfigureAwait(false);
        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedLocal, relativePath, null);
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
        if (options.DryRun)
        {
            Report(result, options, SyncActivityKind.Conflict, relativePath, "Dry run: would preserve both versions.");
            return;
        }

        string? details = null;
        if (local is not null && remoteFile is not null)
        {
            string conflictPath = _localWriter.CreateConflictRelativePath(syncPair.LocalRootPath, relativePath, DateTime.UtcNow);
            await _localWriter.WriteFileAsync(
                syncPair.LocalRootPath,
                conflictPath,
                (stream, token) => _remoteFiles.DownloadFileAsync(remoteFile.Id, stream, token),
                remoteFile.UpdatedAt == default ? null : remoteFile.UpdatedAt,
                cancellationToken).ConfigureAwait(false);
            details = "Remote version saved as " + conflictPath;
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, remoteFile), cancellationToken)
                .ConfigureAwait(false);
        }
        else if (local is not null)
        {
            NodeFileManifestDto uploaded = await _remoteFiles.UploadFileAsync(
                syncPair.RemoteRootNodeId,
                relativePath,
                local,
                null,
                cancellationToken).ConfigureAwait(false);
            details = "Remote deletion conflicted with local change; local version was uploaded again.";
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, local.ContentHash, local.LastWriteUtc, uploaded), cancellationToken)
                .ConfigureAwait(false);
        }
        else if (remoteFile is not null)
        {
            await _localWriter.WriteFileAsync(
                syncPair.LocalRootPath,
                relativePath,
                (stream, token) => _remoteFiles.DownloadFileAsync(remoteFile.Id, stream, token),
                remoteFile.UpdatedAt == default ? null : remoteFile.UpdatedAt,
                cancellationToken).ConfigureAwait(false);
            details = "Local deletion conflicted with remote change; remote version was restored locally.";
            await _stateStore.UpsertAsync(BuildBaseline(syncPair, relativePath, remoteFile.ContentHash, remoteFile.UpdatedAt, remoteFile), cancellationToken)
                .ConfigureAwait(false);
        }

        Report(result, options, SyncActivityKind.Conflict, relativePath, details);
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

    private static bool ContentMatches(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, T> ToDictionary<T>(IEnumerable<T> entries, Func<T, string> pathSelector)
    {
        var result = new Dictionary<string, T>(PathComparer);
        foreach (T entry in entries)
        {
            string path = pathSelector(entry);
            string key = SyncPath.ToKey(path);
            if (result.TryGetValue(key, out T? existing))
            {
                throw new InvalidOperationException(
                    "Sync contains paths that collide after normalization: '"
                    + pathSelector(existing)
                    + "' and '"
                    + path
                    + "'. Rename one of them before syncing.");
            }

            result[key] = entry;
        }

        return result;
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
        string? details)
    {
        var activity = new SyncActivity
        {
            Kind = kind,
            RelativePath = SyncPath.Normalize(relativePath),
            Details = details,
        };
        result.Activities.Add(activity);
        options.ActivityProgress?.Report(activity);
    }

    private static void ValidateOptions(SyncRunOptions options)
    {
        if (options.MaxDeletesPerRun < 0)
        {
            throw new InvalidOperationException("MaxDeletesPerRun cannot be negative.");
        }

        if (options.MaxDeleteRatio is < 0 or > 1)
        {
            throw new InvalidOperationException("MaxDeleteRatio must be between 0 and 1.");
        }

        if (options.DeleteRatioBaselineThreshold < 0)
        {
            throw new InvalidOperationException("DeleteRatioBaselineThreshold cannot be negative.");
        }
    }

    private static void ValidateDeleteSafety(
        SyncRunOptions options,
        IReadOnlyCollection<string> pathKeys,
        Dictionary<string, LocalFileSnapshot> localByPath,
        Dictionary<string, RemoteFileSnapshot> remoteByPath,
        Dictionary<string, SyncStateEntry> stateByPath)
    {
        int plannedDeletes = CountPlannedDataDeletes(pathKeys, localByPath, remoteByPath, stateByPath);
        if (plannedDeletes == 0)
        {
            return;
        }

        int baselineCount = stateByPath.Count;
        bool countExceeded = plannedDeletes > options.MaxDeletesPerRun;
        bool ratioExceeded = baselineCount >= options.DeleteRatioBaselineThreshold
            && baselineCount > 0
            && plannedDeletes > baselineCount * options.MaxDeleteRatio;
        if (!countExceeded && !ratioExceeded)
        {
            return;
        }

        throw new InvalidOperationException(
            "Sync planned "
            + plannedDeletes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " data delete(s) out of "
            + baselineCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " baseline file(s), exceeding the configured safety limits. Verify local and remote paths before retrying.");
    }

    private static int CountPlannedDataDeletes(
        IEnumerable<string> pathKeys,
        Dictionary<string, LocalFileSnapshot> localByPath,
        Dictionary<string, RemoteFileSnapshot> remoteByPath,
        Dictionary<string, SyncStateEntry> stateByPath)
    {
        int count = 0;
        foreach (string key in pathKeys)
        {
            localByPath.TryGetValue(key, out LocalFileSnapshot? local);
            remoteByPath.TryGetValue(key, out RemoteFileSnapshot? remote);
            stateByPath.TryGetValue(key, out SyncStateEntry? state);
            if (state is null)
            {
                continue;
            }

            bool localDeleted = local is null && !string.IsNullOrWhiteSpace(state.LocalContentHash);
            bool remoteDeleted = remote is null && state.RemoteFileId.HasValue;
            bool localChanged = local is not null && !ContentMatches(local.ContentHash, state.LocalContentHash);
            bool remoteChanged = remote is not null && !RemoteMatchesBaseline(remote.File, state);
            bool baselineDiverged = !ContentMatches(state.LocalContentHash, state.RemoteContentHash);
            if (baselineDiverged)
            {
                continue;
            }

            if (localDeleted && !remoteChanged && remote is not null)
            {
                count++;
                continue;
            }

            if (remoteDeleted && !localChanged && local is not null)
            {
                count++;
            }
        }

        return count;
    }
}
