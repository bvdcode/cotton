// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cotton.Storage.Processors
{
    public class FileSystemStorageProcessor(ILogger<FileSystemStorageProcessor> _logger) : IStorageProcessor
    {
        public int Priority => 10;
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "files";
        private const int MinFileUidLength = 6;
        private readonly string _basePath = Path.Combine(AppContext.BaseDirectory, BaseDirectoryName);

        private string GetFolderByUid(string uid)
        {
            uid = NormalizeIdentity(uid);
            string p1 = uid[..2];
            string p2 = uid[2..4];
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

        private static string NormalizeIdentity(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            string normalized = uid.Trim().ToLowerInvariant();
            if (normalized.Length < MinFileUidLength)
            {
                throw new ArgumentException("File UID is too short, minimum length is " + MinFileUidLength);
            }
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z');
                if (!isHex)
                {
                    throw new ArgumentException("File UID contains invalid character: " + c);
                }
            }
            return normalized;
        }

        public async Task<Stream> ReadAsync(string uid, Stream stream)
        {
            if (stream != Stream.Null)
            {
                throw new NotSupportedException("This processor does not support chained reading.");
            }
            uid = NormalizeIdentity(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, uid[4..] + ChunkFileExtension);
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

        public async Task<Stream> WriteAsync(string uid, Stream stream)
        {
            const int WriteBufferSize = 1024 * 1024;

            uid = NormalizeIdentity(uid);
            ArgumentNullException.ThrowIfNull(stream);

            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, uid[4..] + ChunkFileExtension);
            if (File.Exists(filePath))
            {
                _logger.LogCritical("File collision detected for file {Uid}: two different files have the same name", uid);
                throw new IOException("File collision detected: two different files have the same name: " + uid);
            }

            string tmpFilePath = Path.Combine(dirPath, $"{uid[4..]}.{Guid.NewGuid():N}.tmp");
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
                File.Move(tmpFilePath, filePath);
                File.SetAttributes(filePath, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
            }
            catch (Exception)
            {
                TryDelete(tmpFilePath);
                throw;
            }
            return Stream.Null;
        }
    }
}
