// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{
    /// <summary>
    /// Contains the activities emitted by one synchronization pass.
    /// </summary>
    public class SyncRunResult
    {
        private string? _actionRequiredMessage;
        private bool _requiresUserAction;
        private int _totalActivityCount;

        /// <summary>
        /// Gets the activities emitted during the pass.
        /// </summary>
        public List<SyncActivity> Activities { get; } = [];

        /// <summary>
        /// Gets the total number of activities emitted during the pass.
        /// </summary>
        public int TotalActivityCount => Math.Max(_totalActivityCount, Activities.Count);

        /// <summary>
        /// Gets a value indicating whether the retained activity list was capped.
        /// </summary>
        public bool IsActivityListTruncated { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the pass stopped on a condition that needs user review.
        /// </summary>
        public bool RequiresUserAction => _requiresUserAction || Activities.Any(static activity => activity.RequiresUserAction);

        /// <summary>
        /// Gets the first user-review message reported by the pass.
        /// </summary>
        public string? ActionRequiredMessage => _actionRequiredMessage
            ?? Activities.FirstOrDefault(static activity => activity.RequiresUserAction)?.Details;

        /// <summary>
        /// Records an emitted activity while keeping the retained result history bounded.
        /// </summary>
        public void RecordActivity(SyncActivity activity, int maximumStoredActivities)
        {
            ArgumentNullException.ThrowIfNull(activity);
            ArgumentOutOfRangeException.ThrowIfNegative(maximumStoredActivities);

            _totalActivityCount++;
            if (activity.RequiresUserAction)
            {
                _requiresUserAction = true;
                _actionRequiredMessage ??= activity.Details;
            }

            if (Activities.Count < maximumStoredActivities)
            {
                Activities.Add(activity);
                return;
            }

            IsActivityListTruncated = true;
        }
    }
}
