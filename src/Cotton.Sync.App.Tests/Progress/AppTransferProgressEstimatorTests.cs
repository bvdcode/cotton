// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Progress;

namespace Cotton.Sync.App.Tests.Progress;

public sealed class AppTransferProgressEstimatorTests
{
    [Test]
    public void AddSample_CalculatesRollingSpeedAndRemainingTime()
    {
        var estimator = new AppTransferProgressEstimator();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        AppTransferProgressEstimate first = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/file.bin",
            transferredBytes: 0,
            totalBytes: 10_000,
            isCompleted: false,
            startedAtUtc);
        AppTransferProgressEstimate second = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/file.bin",
            transferredBytes: 2_000,
            totalBytes: 10_000,
            isCompleted: false,
            startedAtUtc.AddSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(first.SpeedBytesPerSecond, Is.Null);
            Assert.That(first.EstimatedTimeRemaining, Is.Null);
            Assert.That(second.SpeedBytesPerSecond, Is.EqualTo(1_000).Within(0.01));
            Assert.That(second.EstimatedTimeRemaining, Is.EqualTo(TimeSpan.FromSeconds(8)));
        });
    }

    [Test]
    public void AddSample_UsesRollingWindowInsteadOfWholeTransferDuration()
    {
        var estimator = new AppTransferProgressEstimator();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        _ = estimator.AddSample(
            AppTransferDirection.Download,
            "Reports/file.bin",
            transferredBytes: 0,
            totalBytes: 100_000,
            isCompleted: false,
            startedAtUtc);
        _ = estimator.AddSample(
            AppTransferDirection.Download,
            "Reports/file.bin",
            transferredBytes: 1_000,
            totalBytes: 100_000,
            isCompleted: false,
            startedAtUtc.AddSeconds(6));
        AppTransferProgressEstimate latest = estimator.AddSample(
            AppTransferDirection.Download,
            "Reports/file.bin",
            transferredBytes: 7_000,
            totalBytes: 100_000,
            isCompleted: false,
            startedAtUtc.AddSeconds(8));

        Assert.Multiple(() =>
        {
            Assert.That(latest.SpeedBytesPerSecond, Is.EqualTo(3_000).Within(0.01));
            Assert.That(latest.EstimatedTimeRemaining, Is.EqualTo(TimeSpan.FromSeconds(31)));
        });
    }

    [Test]
    public void AddSample_ResetsWhenTransferChanges()
    {
        var estimator = new AppTransferProgressEstimator();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        _ = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/first.bin",
            transferredBytes: 0,
            totalBytes: 10_000,
            isCompleted: false,
            startedAtUtc);
        _ = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/first.bin",
            transferredBytes: 5_000,
            totalBytes: 10_000,
            isCompleted: false,
            startedAtUtc.AddSeconds(1));
        AppTransferProgressEstimate nextTransfer = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/second.bin",
            transferredBytes: 2_000,
            totalBytes: 10_000,
            isCompleted: false,
            startedAtUtc.AddSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(nextTransfer.SpeedBytesPerSecond, Is.Null);
            Assert.That(nextTransfer.EstimatedTimeRemaining, Is.Null);
        });
    }

    [Test]
    public void AddSample_DoesNotReportSpeedForCompletionSample()
    {
        var estimator = new AppTransferProgressEstimator();
        DateTime startedAtUtc = new(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

        _ = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/file.bin",
            transferredBytes: 0,
            totalBytes: 10_000,
            isCompleted: false,
            startedAtUtc);
        AppTransferProgressEstimate completed = estimator.AddSample(
            AppTransferDirection.Upload,
            "Reports/file.bin",
            transferredBytes: 10_000,
            totalBytes: 10_000,
            isCompleted: true,
            startedAtUtc.AddSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(completed.SpeedBytesPerSecond, Is.Null);
            Assert.That(completed.EstimatedTimeRemaining, Is.Null);
        });
    }
}
