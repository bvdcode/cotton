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
        var deleteBudget = new SyncDeleteBudget(options);

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

            await ReconcileWithBaselineAsync(syncPair, options, result, deleteBudget, state, relativePath, local, remote, cancellationToken)
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
        SyncDeleteBudget deleteBudget,
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
            await DeleteRemoteAsync(syncPair, options, result, deleteBudget, relativePath, remote.File, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (remoteDeleted && !localChanged && local is not null)
        {
            await DeleteLocalAsync(syncPair, options, result, deleteBudget, relativePath, cancellationToken).ConfigureAwait(false);
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
        SyncDeleteBudget deleteBudget,
        string relativePath,
        NodeFileManifestDto remoteFile,
        CancellationToken cancellationToken)
    {
        if (!deleteBudget.TryConsumeRemoteDelete(out string? details))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, details);
            return;
        }

        await _remoteFiles.DeleteFileAsync(
            remoteFile.Id,
            options.DeleteRemotePermanently,
            remoteFile.ETag,
            cancellationToken).ConfigureAwait(false);
        await _stateStore.DeleteAsync(syncPair.SyncPairId, relativePath, cancellationToken).ConfigureAwait(false);
        Report(result, options, SyncActivityKind.DeletedRemote, relativePath, null);
    }

    private async Task DeleteLocalAsync(
        SyncPair syncPair,
        SyncRunOptions options,
        SyncRunResult result,
        SyncDeleteBudget deleteBudget,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (!deleteBudget.TryConsumeLocalDelete(out string? details))
        {
            Report(result, options, SyncActivityKind.Skipped, relativePath, details);
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
            result[SyncPath.ToKey(pathSelector(entry))] = entry;
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

    private sealed class SyncDeleteBudget
    {
        private readonly int _maximumLocalDeletes;
        private readonly int _maximumRemoteDeletes;
        private int _localDeletes;
        private int _remoteDeletes;

        public SyncDeleteBudget(SyncRunOptions options)
        {
            _maximumLocalDeletes = options.MaximumLocalDeletesPerRun;
            _maximumRemoteDeletes = options.MaximumRemoteDeletesPerRun;
        }

        public bool TryConsumeLocalDelete(out string? details)
        {
            return TryConsume(
                ref _localDeletes,
                _maximumLocalDeletes,
                "Local delete blocked by mass-delete guard.",
                out details);
        }

        public bool TryConsumeRemoteDelete(out string? details)
        {
            return TryConsume(
                ref _remoteDeletes,
                _maximumRemoteDeletes,
                "Remote delete blocked by mass-delete guard.",
                out details);
        }

        private static bool TryConsume(
            ref int used,
            int maximum,
            string blockedDetails,
            out string? details)
        {
            if (used >= maximum)
            {
                details = blockedDetails;
                return false;
            }

            used++;
            details = null;
            return true;
        }
    }
}
