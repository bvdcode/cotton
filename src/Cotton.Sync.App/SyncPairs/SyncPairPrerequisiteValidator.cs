// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{
    /// <summary>
    /// Validates sync-pair prerequisites that require local or remote I/O.
    /// </summary>
    public class SyncPairPrerequisiteValidator : ISyncPairPrerequisiteValidator
    {
        private readonly ILocalSyncRootProbe _localRoots;
        private readonly IRemoteSyncRootProbe _remoteRoots;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncPairPrerequisiteValidator" /> class.
        /// </summary>
        public SyncPairPrerequisiteValidator(ILocalSyncRootProbe localRoots, IRemoteSyncRootProbe remoteRoots)
        {
            _localRoots = localRoots ?? throw new ArgumentNullException(nameof(localRoots));
            _remoteRoots = remoteRoots ?? throw new ArgumentNullException(nameof(remoteRoots));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SyncPairValidationError>> ValidateAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(syncPair);
            var errors = new List<SyncPairValidationError>();
            if (!await _localRoots.CanUseAsync(syncPair.LocalRootPath, cancellationToken).ConfigureAwait(false))
            {
                errors.Add(new SyncPairValidationError(
                    SyncPairValidationIssue.LocalRootUnavailable,
                    syncPair.Id,
                    null,
                    "The local sync root does not exist and cannot be created or accessed."));
            }

            if (!await _remoteRoots.ExistsAsync(syncPair.RemoteRootNodeId, cancellationToken).ConfigureAwait(false))
            {
                errors.Add(new SyncPairValidationError(
                    SyncPairValidationIssue.RemoteRootUnavailable,
                    syncPair.Id,
                    null,
                    "The remote sync root cannot be resolved."));
            }

            return errors;
        }
    }
}
