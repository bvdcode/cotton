// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;
using Microsoft.Extensions.Logging;

namespace Cotton.Storage.Backends
{
    /// <summary>
    /// Filesystem backend that stores opaque Cotton chunks under a sharded directory layout.
    /// </summary>
    public class FileSystemStorageBackend(ILogger<FileSystemStorageBackend> _logger, string? basePath = null) : IStorageBackend, IStorageCapacityReporter
    {
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "files";
        private const string TempDirectoryName = "tmp";
        private readonly string _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, BaseDirectoryName);

        /// <inheritdoc />
        public StorageCapacitySnapshot GetCapacitySnapshot()
        {
            Directory.CreateDirectory(_basePath);
            string rootPath = Path.GetFullPath(_basePath);
            var drive = ResolveDrive(rootPath);

            return new StorageCapacitySnapshot(
                Backend: "filesystem",
                RootPath: rootPath,
                TotalBytes: drive.TotalSize,
                AvailableBytes: drive.AvailableFreeSpace);
        }

        private static DriveInfo ResolveDrive(string rootPath)
        {
            string normalizedRootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            DriveInfo? drive = DriveInfo.GetDrives()
                .Where(candidate => normalizedRootPath.StartsWith(
                    candidate.RootDirectory.FullName,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                .OrderByDescending(candidate => candidate.RootDirectory.FullName.Length)
                .FirstOrDefault();

            if (drive is not null)
            {
                return drive;
            }

            string? pathRoot = Path.GetPathRoot(rootPath);
            if (string.IsNullOrWhiteSpace(pathRoot))
            {
                throw new InvalidOperationException($"Cannot resolve storage drive for path {rootPath}.");
            }

            return new DriveInfo(pathRoot);
        }

        private string GetTempDirectory()
        {
            string dirPath = Path.Combine(_basePath, TempDirectoryName);
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }

        private string CreateTempFilePath(string fileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            string tmpDir = GetTempDirectory();
            return Path.Combine(tmpDir, $"{fileName}.{Guid.NewGuid():N}.tmp");
        }

        /// <inheritdoc />
        public void CleanupTempFiles(TimeSpan ttl)
        {
            try
            {
                string tmpDir = GetTempDirectory();
                var cutoff = DateTimeOffset.UtcNow - ttl;

                foreach (var file in Directory.EnumerateFiles(tmpDir, "*.tmp", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        var lastWrite = info.LastWriteTimeUtc;
                        if (lastWrite <= cutoff.UtcDateTime)
                        {
                            info.Attributes = FileAttributes.Normal;
                            info.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temp file {Path}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory");
            }
        }

        private string GetFolderByUid(string uid)
        {
            var (p1, p2, _) = StorageKeyHelper.GetSegments(uid);
            string dirPath = Path.Combine(_basePath, p1, p2);
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {Path}", path);
            }
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);

            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);

            if (!File.Exists(filePath))
            {
                return Task.FromResult(false);
            }

            try
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {Uid}", uid);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<Stream> ReadAsync(string uid)
        {
            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }
            var fso = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Access = FileAccess.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };
            return Task.FromResult<Stream>(new FileStream(filePath, fso));
        }

        /// <inheritdoc />
        public async Task WriteAsync(string uid, Stream stream)
        {
            const int WriteBufferSize = 2 * 1024 * 1024;

            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            ArgumentNullException.ThrowIfNull(stream);

            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);
            if (File.Exists(filePath))
            {
                _logger.LogDebug("File {Uid} deduplicated, skipping write", uid);
                return;
            }

            string tmpFilePath = CreateTempFilePath(fileName);
            var fso = new FileStreamOptions
            {
                Share = FileShare.None,
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                BufferSize = WriteBufferSize,
                Options = FileOptions.Asynchronous,
            };

            try
            {
                _logger.LogDebug("Storing new file {Uid}", uid);
                await using var tmp = new FileStream(tmpFilePath, fso);
                if (stream.CanSeek)
                {
                    stream.Seek(default, SeekOrigin.Begin);
                }
                await stream.CopyToAsync(tmp, WriteBufferSize).ConfigureAwait(false);
                await tmp.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                TryDelete(tmpFilePath);
                throw;
            }

            try
            {
                File.Move(tmpFilePath, filePath, overwrite: false);
                File.SetAttributes(filePath, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
            }
            catch (IOException ex) when (File.Exists(filePath))
            {
                _logger.LogDebug(ex, "File {Uid} was written concurrently, deduplicated temp write", uid);
                TryDelete(tmpFilePath);
            }
            catch (Exception)
            {
                TryDelete(tmpFilePath);
                throw;
            }
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string uid)
        {
            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);
            return Task.FromResult(File.Exists(filePath));
        }

        /// <inheritdoc />
        public Task<long> GetSizeAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);

            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);

            if (!File.Exists(filePath))
            {
                return Task.FromResult(0L);
            }

            var info = new FileInfo(filePath);
            return Task.FromResult(info.Length);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<string> ListAllKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!Directory.Exists(_basePath))
            {
                yield break;
            }

            foreach (string filePath in Directory.EnumerateFiles(_basePath, "*" + ChunkFileExtension, SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(_basePath, filePath);
                string[] parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Length != 3)
                {
                    continue;
                }

                string p1 = parts[0];
                string p2 = parts[1];
                string fileName = Path.GetFileNameWithoutExtension(parts[2]);
                string uid = p1 + p2 + fileName;

                yield return uid;
            }
        }
    }
}
