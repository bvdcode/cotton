// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Tests.ViewModels;

public sealed class SyncPairRowViewModelTests
{
    [Test]
    public void StatusIndicator_HidesDecorativeIdleAndClassifiesVisibleStates()
    {
        var row = new SyncPairRowViewModel
        {
            DisplayName = "Videos",
            Status = " idle ",
        };

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.Empty);
            Assert.That(row.HasDisplayStatus, Is.False);
            Assert.That(row.IsStatusIndicatorVisible, Is.False);
        });

        row.Status = " Syncing ";

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.EqualTo("Syncing"));
            Assert.That(row.HasDisplayStatus, Is.True);
            Assert.That(row.IsStatusIndicatorVisible, Is.True);
            Assert.That(row.IsStatusActive, Is.True);
            Assert.That(row.IsStatusPaused, Is.False);
            Assert.That(row.IsStatusAttention, Is.False);
        });

        row.Status = "Paused";

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.EqualTo("Paused"));
            Assert.That(row.IsStatusActive, Is.False);
            Assert.That(row.IsStatusPaused, Is.True);
            Assert.That(row.IsStatusAttention, Is.False);
        });

        row.Status = "Error";

        Assert.Multiple(() =>
        {
            Assert.That(row.DisplayStatus, Is.EqualTo("Error"));
            Assert.That(row.IsStatusAttention, Is.True);
        });
    }
}
