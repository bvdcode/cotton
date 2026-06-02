// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text;
using Cotton.Sync.Local;

namespace Cotton.Sync.Tests.Local;

public sealed class AtomicLocalFileSyncWriterTests
{
    private string _tempDirectory = string.Empty;
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-local-writer-tests", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_tempDirectory, "root");
        Directory.CreateDirectory(_root);
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
    public async Task WriteFileAsync_WritesInsideSyncRoot()
    {
        var writer = new AtomicLocalFileSyncWriter();

        await writer.WriteFileAsync(
            _root,
            "docs/file.txt",
            static (stream, cancellationToken) => stream.WriteAsync(
                Encoding.UTF8.GetBytes("inside"),
                cancellationToken).AsTask());

        Assert.That(File.ReadAllText(Path.Combine(_root, "docs", "file.txt")), Is.EqualTo("inside"));
    }

    [Test]
    public async Task WriteFileAsync_RejectsTraversalAndDoesNotWriteOutsideRoot()
    {
        var writer = new AtomicLocalFileSyncWriter();
        string outsidePath = Path.Combine(_tempDirectory, "outside.txt");

        Assert.ThrowsAsync<ArgumentException>(() => writer.WriteFileAsync(
            _root,
            "../outside.txt",
            static (stream, cancellationToken) => stream.WriteAsync(
                Encoding.UTF8.GetBytes("outside"),
                cancellationToken).AsTask()));

        await Task.Yield();
        Assert.That(File.Exists(outsidePath), Is.False);
    }

    [Test]
    public async Task DeleteFileAsync_RejectsTraversalAndLeavesOutsideFileUntouched()
    {
        var writer = new AtomicLocalFileSyncWriter();
        string outsidePath = Path.Combine(_tempDirectory, "outside.txt");
        File.WriteAllText(outsidePath, "keep");

        Assert.ThrowsAsync<ArgumentException>(() => writer.DeleteFileAsync(_root, "../outside.txt"));

        await Task.Yield();
        Assert.That(File.ReadAllText(outsidePath), Is.EqualTo("keep"));
    }
}
