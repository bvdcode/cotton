// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;
using Microsoft.Extensions.Logging;

namespace Cotton.Storage.Backends
{
    public class FileSystemStorageBackend(ILogger<FileSystemStorageBackend> _logger) : IStorageBackend
    {
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "files";
        private readonly string _basePath = Path.Combine(AppContext.BaseDirectory, BaseDirectoryName);

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

        public async Task<Stream> ReadAsync(string uid)
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
            return new FileStream(filePath, fso);
        }

        public async Task WriteAsync(string uid, Stream stream)
        {
            const int WriteBufferSize = 2 * 1024 * 1024;

            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            ArgumentNullException.ThrowIfNull(stream);

            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);
            if (File.Exists(filePath))
            {
                _logger.LogInformation("File {Uid} deduplicated, skipping write", uid);
                return;
            }

            string tmpDir = Path.Combine(Path.GetTempPath(), "cotton", "upload-chunks");
            Directory.CreateDirectory(tmpDir);
            string tmpFilePath = Path.Combine(tmpDir, $"{fileName}.{Guid.NewGuid():N}.tmp");
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
                _logger.LogInformation("Storing new file {Uid}", uid);
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
            catch (Exception)
            {
                TryDelete(tmpFilePath);
                throw;
            }
        }

        public Task<bool> ExistsAsync(string uid)
        {
            var (_, _, fileName) = StorageKeyHelper.GetSegments(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, fileName + ChunkFileExtension);
            return Task.FromResult(File.Exists(filePath));
        }
    }
}
