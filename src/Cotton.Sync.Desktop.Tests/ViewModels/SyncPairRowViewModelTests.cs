// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Tests.ViewModels;

public sealed class SyncPairRowViewModelTests
{
    [Test]
    public void IsHeaderStatusVisible_HidesDecorativeIdleAndExpandedEditorStatus()
    {
        var row = new SyncPairRowViewModel
        {
            Status = "Idle",
        };

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.Empty);
            Assert.That(row.HasDisplayStatus, Is.False);
            Assert.That(row.IsHeaderStatusVisible, Is.False);
        });

        row.Status = "Syncing";

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.EqualTo("Syncing"));
            Assert.That(row.HasDisplayStatus, Is.True);
            Assert.That(row.IsHeaderStatusVisible, Is.True);
            Assert.That(row.IsErrorStatus, Is.False);
        });

        row.Status = "Error";

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.EqualTo("Error"));
            Assert.That(row.IsErrorStatus, Is.True);
        });

        row.IsEditorVisible = true;

        Assert.That(row.IsHeaderStatusVisible, Is.False);
    }
}
