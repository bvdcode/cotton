// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Sync.Local;

namespace Cotton.Sync.Tests.Local;

public sealed class LocalFileScannerTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cotton-local-scan", Guid.NewGuid().ToString("N"));
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
    public async Task ScanAsync_ReturnsNestedFilesWithNormalizedPathsAndHashes()
    {
        WriteFile("alpha.txt", "alpha");
        WriteFile(Path.Combine("Docs", "Report.txt"), "report");
        var scanner = new LocalFileScanner();

        IReadOnlyList<LocalFileSnapshot> files = await scanner.ScanAsync(_root);

        Assert.Multiple(() =>
        {
            Assert.That(files.Select(x => x.RelativePath), Is.EqualTo(new[] { "alpha.txt", "Docs/Report.txt" }));
            Assert.That(files.Single(x => x.RelativePath == "alpha.txt").ContentHash, Is.EqualTo(Hash("alpha")));
            Assert.That(files.Single(x => x.RelativePath == "Docs/Report.txt").SizeBytes, Is.EqualTo(6));
            Assert.That(files.All(x => x.LastWriteUtc.Kind == DateTimeKind.Utc), Is.True);
        });
    }

    [Test]
    public async Task ScanAsync_IgnoresTempFilesAndCottonWorkingFolder()
    {
        WriteFile("keep.txt", "keep");
        WriteFile("upload.tmp", "tmp");
        WriteFile("download.partial", "partial");
        WriteFile("chrome.crdownload", "partial");
        WriteFile("~$office.docx", "office");
        WriteFile("backup~", "backup");
        WriteFile(Path.Combine(".cotton-sync", "state.tmp"), "state");
        var scanner = new LocalFileScanner();

        IReadOnlyList<LocalFileSnapshot> files = await scanner.ScanAsync(_root);

        Assert.That(files.Select(x => x.RelativePath), Is.EqualTo(new[] { "keep.txt" }));
    }

    [Test]
    public async Task ScanAsync_IgnoresSymlinkFilesAndDoesNotTraverseSymlinkDirectories()
    {
        WriteFile("target.txt", "target");
        WriteFile(Path.Combine("real-dir", "inside.txt"), "inside");
        string fileLinkPath = Path.Combine(_root, "target-link.txt");
        string directoryLinkPath = Path.Combine(_root, "real-dir-link");
        TryCreateFileSymlink(fileLinkPath, Path.Combine(_root, "target.txt"));
        TryCreateDirectorySymlink(directoryLinkPath, Path.Combine(_root, "real-dir"));
        var scanner = new LocalFileScanner();

        IReadOnlyList<LocalFileSnapshot> files = await scanner.ScanAsync(_root);

        Assert.That(files.Select(x => x.RelativePath), Is.EqualTo(new[] { "real-dir/inside.txt", "target.txt" }));
    }

    [Test]
    public async Task ScanAsync_ThrowsForLockedFile()
    {
        WriteFile("keep.txt", "keep");
        WriteFile("locked.txt", "locked");
        await using FileStream locked = new(
            FullPath("locked.txt"),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var scanner = new LocalFileScanner();

        LocalFileUnavailableException? exception = Assert.ThrowsAsync<LocalFileUnavailableException>(() => scanner.ScanAsync(_root));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.RelativePath, Is.EqualTo("locked.txt"));
            Assert.That(exception.FullPath, Is.EqualTo(FullPath("locked.txt")));
            Assert.That(exception.InnerException, Is.TypeOf<IOException>());
        });
    }

    [Test]
    public void ScanAsync_RejectsMissingRoot()
    {
        var scanner = new LocalFileScanner();
        string missing = Path.Combine(_root, "missing");

        Assert.ThrowsAsync<DirectoryNotFoundException>(() => scanner.ScanAsync(missing));
    }

    private void WriteFile(string relativePath, string text)
    {
        string path = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc));
    }

    private string FullPath(string relativePath)
    {
        return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void TryCreateFileSymlink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Ignore("File symlink creation is unavailable in this test environment: " + ex.Message);
        }
    }

    private static void TryCreateDirectorySymlink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Ignore("Directory symlink creation is unavailable in this test environment: " + ex.Message);
        }
    }

    private static string Hash(string text)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
