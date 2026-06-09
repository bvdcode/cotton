// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{
    internal class SyncDeleteGuard
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
