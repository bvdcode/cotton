// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text;
using Cotton.Sync.Local;

namespace Cotton.Sync.Tests.Local;

public sealed class AtomicLocalFileSyncWriterTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cotton-local-writer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task WriteFileAsync_RemovesTemporaryFileWhenDownloadFailsAndPreservesExistingFile()
    {
        string relativePath = "Docs/file.txt";
        WriteFile(relativePath, "existing");
        var writer = new AtomicLocalFileSyncWriter();

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await writer.WriteFileAsync(
                _root,
                relativePath,
                async (stream, cancellationToken) =>
                {
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"), cancellationToken);
                    throw new InvalidOperationException("download failed");
                }));

        string temporaryDirectory = Path.Combine(_root, ".cotton-sync", "tmp");
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(ReadFile(relativePath), Is.EqualTo("existing"));
            Assert.That(
                Directory.Exists(temporaryDirectory)
                    ? Directory.GetFiles(temporaryDirectory, "*", SearchOption.AllDirectories)
                    : [],
                Is.Empty);
        });
    }

    [Test]
    public async Task DeleteFileAsync_MovesFileToDeletedQuarantine()
    {
        string relativePath = "Docs/file.txt";
        WriteFile(relativePath, "deleted-content");
        var writer = new AtomicLocalFileSyncWriter();

        await writer.DeleteFileAsync(_root, relativePath);

        string[] deletedFiles = Directory.GetFiles(
            Path.Combine(_root, ".cotton-sync", "deleted"),
            "file.txt",
            SearchOption.AllDirectories);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(FullPath(relativePath)), Is.False);
            Assert.That(deletedFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(deletedFiles[0]), Is.EqualTo("deleted-content"));
            Assert.That(Directory.Exists(Path.Combine(_root, "Docs")), Is.False);
        });
    }

    [Test]
    public void CreateConflictRelativePath_UsesIndexedSuffixWhenTimestampNameExists()
    {
        var writer = new AtomicLocalFileSyncWriter();
        DateTime timestamp = new(2026, 6, 3, 12, 30, 0, DateTimeKind.Utc);
        string firstConflictPath = writer.CreateConflictRelativePath(_root, "Docs/file.txt", timestamp);
        WriteFile(firstConflictPath, "first conflict");

        string secondConflictPath = writer.CreateConflictRelativePath(_root, "Docs/file.txt", timestamp);

        Assert.Multiple(() =>
        {
            Assert.That(firstConflictPath, Is.EqualTo("Docs/file (Cotton conflict 20260603T123000Z).txt"));
            Assert.That(secondConflictPath, Is.EqualTo("Docs/file (Cotton conflict 20260603T123000Z-2).txt"));
            Assert.That(File.Exists(FullPath(firstConflictPath)), Is.True);
            Assert.That(File.Exists(FullPath(secondConflictPath)), Is.False);
        });
    }

    private string FullPath(string relativePath)
    {
        return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ReadFile(string relativePath)
    {
        return File.ReadAllText(FullPath(relativePath));
    }

    private void WriteFile(string relativePath, string content)
    {
        string fullPath = FullPath(relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
