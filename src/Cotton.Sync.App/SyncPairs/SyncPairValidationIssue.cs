// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{

    /// <summary>
    /// Identifies a validation problem in a sync-pair configuration.
    /// </summary>
    public enum SyncPairValidationIssue
    {
        /// <summary>
        /// The sync pair identifier is empty.
        /// </summary>
        EmptyId,

        /// <summary>
        /// The display name is empty.
        /// </summary>
        EmptyDisplayName,

        /// <summary>
        /// The local root path is empty.
        /// </summary>
        EmptyLocalRootPath,

        /// <summary>
        /// The remote root node identifier is empty.
        /// </summary>
        EmptyRemoteRootNodeId,

        /// <summary>
        /// The remote display path is empty.
        /// </summary>
        EmptyRemoteDisplayPath,

        /// <summary>
        /// The selected synchronization mode is reserved for a later implementation.
        /// </summary>
        UnsupportedMode,

        /// <summary>
        /// Two configured sync pairs use the same or nested local roots.
        /// </summary>
        OverlappingLocalRoots,

        /// <summary>
        /// The local root does not exist and cannot be created or accessed.
        /// </summary>
        LocalRootUnavailable,

        /// <summary>
        /// The remote root node cannot be resolved.
        /// </summary>
        RemoteRootUnavailable,
    }
}
