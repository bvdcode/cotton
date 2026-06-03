// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Cli;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli.Tests;

public sealed class SyncCliCommandRunnerTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_PrintsHelpForEmptyArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync([], output, error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("state-summary"));
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task RunAsync_ReturnsErrorForMissingStateSummaryArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(["state-summary"], output, error);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Does.Contain("--database"));
            Assert.That(error.ToString(), Does.Contain("--sync-pair"));
        });
    }

    [Test]
    public async Task StateSummary_PrintsEntryCountAndCursor()
    {
        string databasePath = Path.Combine(_tempDirectory, "sync-state.db");
        string syncPairId = Guid.NewGuid().ToString("D");
        var store = new SqliteSyncStateStore(databasePath);
        await store.InitializeAsync();
        await store.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = syncPairId,
            RelativePath = "Documents/report.txt",
            Kind = SyncEntryKind.File,
            SyncedAtUtc = DateTime.UtcNow,
        });
        await store.SaveChangeCursorAsync(new SyncChangeCursor
        {
            SyncPairId = syncPairId,
            LastCursor = 42,
            UpdatedAtUtc = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
        });
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await SyncCliCommandRunner.RunAsync(
            ["state-summary", "--database", databasePath, "--sync-pair", syncPairId],
            output,
            error);

        string text = output.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(text, Does.Contain("Entries: 1"));
            Assert.That(text, Does.Contain("Remote cursor: 42"));
            Assert.That(text, Does.Contain(syncPairId));
        });
    }
}
