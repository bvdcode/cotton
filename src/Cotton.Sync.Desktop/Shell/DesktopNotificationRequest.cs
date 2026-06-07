// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed record DesktopNotificationRequest
{
    public DesktopNotificationRequest(
        DesktopNotificationKind kind,
        Guid syncPairId,
        string title,
        string message)
    {
        if (kind == DesktopNotificationKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Desktop notification kind must be known.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Kind = kind;
        SyncPairId = syncPairId;
        Title = DesktopUserMessageFormatter.Compact(title, DesktopUserMessageFormatter.TitleMaxLength);
        Message = DesktopUserMessageFormatter.Compact(message);
    }

    public DesktopNotificationKind Kind { get; }

    public Guid SyncPairId { get; }

    public string Title { get; }

    public string Message { get; }
}
