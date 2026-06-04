// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed class DesktopNotificationTracker
{
    private readonly HashSet<Guid> _initialSyncCompleted = [];
    private readonly Dictionary<Guid, string?> _previousErrors = [];
    private readonly Dictionary<Guid, string> _previousStatuses = [];

    public IReadOnlyList<DesktopNotificationRequest> Apply(
        DesktopSyncStatusSnapshot status,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(displayNames);
        var notifications = new List<DesktopNotificationRequest>();
        foreach (DesktopSyncPairStatusSnapshot pair in status.SyncPairs)
        {
            string previousStatus = _previousStatuses.GetValueOrDefault(pair.Id, string.Empty);
            string displayName = displayNames.GetValueOrDefault(pair.Id, "Sync folder");
            string? previousError = _previousErrors.GetValueOrDefault(pair.Id);
            AddNotificationIfNeeded(notifications, pair, previousStatus, previousError, displayName);
            _previousStatuses[pair.Id] = pair.Status;
            _previousErrors[pair.Id] = pair.LastError;
        }

        return notifications;
    }

    public void Reset()
    {
        _initialSyncCompleted.Clear();
        _previousErrors.Clear();
        _previousStatuses.Clear();
    }

    private void AddNotificationIfNeeded(
        List<DesktopNotificationRequest> notifications,
        DesktopSyncPairStatusSnapshot pair,
        string previousStatus,
        string? previousError,
        string displayName)
    {
        if (string.Equals(pair.Status, "Error", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(pair.LastError))
        {
            if (!string.Equals(previousStatus, pair.Status, StringComparison.Ordinal)
                || !string.Equals(previousError, pair.LastError, StringComparison.Ordinal))
            {
                notifications.Add(new DesktopNotificationRequest(
                    DesktopNotificationKind.ActionRequiredError,
                    pair.Id,
                    "Action required",
                    displayName + ": " + pair.LastError));
            }

            return;
        }

        if (string.Equals(pair.Status, "Conflict", StringComparison.Ordinal)
            && !string.Equals(previousStatus, pair.Status, StringComparison.Ordinal))
        {
            notifications.Add(new DesktopNotificationRequest(
                DesktopNotificationKind.Conflict,
                pair.Id,
                "Conflict detected",
                displayName + " has files that need attention."));
            return;
        }

        if (!_initialSyncCompleted.Contains(pair.Id)
            && string.Equals(pair.Status, "Idle", StringComparison.Ordinal)
            && pair.LastSyncedAtUtc.HasValue
            && IsSyncingLike(previousStatus))
        {
            _initialSyncCompleted.Add(pair.Id);
            notifications.Add(new DesktopNotificationRequest(
                DesktopNotificationKind.InitialSyncComplete,
                pair.Id,
                "Initial sync complete",
                displayName + " is up to date."));
        }
    }

    private static bool IsSyncingLike(string status)
    {
        return string.Equals(status, "Syncing", StringComparison.Ordinal)
            || string.Equals(status, "Scanning", StringComparison.Ordinal)
            || string.Equals(status, "Sync requested", StringComparison.Ordinal);
    }
}
