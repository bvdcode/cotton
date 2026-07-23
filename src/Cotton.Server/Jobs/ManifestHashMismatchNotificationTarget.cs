// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Identifies one file owner who must be notified about a manifest hash mismatch.
    /// </summary>
    internal record ManifestHashMismatchNotificationTarget(Guid OwnerId, string FileName);
}
