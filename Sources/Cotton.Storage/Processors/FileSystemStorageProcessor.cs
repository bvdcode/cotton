using Cotton.Storage.Abstractions;
using Cotton.Storage.Streams;
using System;
using System.Collections.Generic;
using System.Runtime;
using System.Text;

namespace Cotton.Storage.Processors
{
    internal class FileSystemStorageProcessor : IStorageProcessor
    {
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "files";
        private const int MinFileUidLength = 6;

        private readonly string _basePath;

        public FileSystemStorageProcessor()
        {

            _basePath = Path.Combine(AppContext.BaseDirectory, BaseDirectoryName);
            Directory.CreateDirectory(_basePath);
        }

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

        public async Task<Stream> GetFileReadStream(string uid, CancellationToken ct = default)
        {
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
            var fileStream = new FileStream(filePath, fso);
            var decryptedStream = new MemoryStream(capacity: (int)fileStream.Length);
            await _cipher.DecryptAsync(fileStream, decryptedStream, ct).ConfigureAwait(false);
            decryptedStream.Seek(default, SeekOrigin.Begin);
            await fileStream.DisposeAsync().ConfigureAwait(false);
            return decryptedStream;
        }

        public async Task WriteFileAsync(string uid, Stream stream, CancellationToken ct = default)
        {
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
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };

            try
            {
                _logger.LogInformation("Storing new file {Uid}", uid);
                await using var tmp = new FileStream(tmpFilePath, fso);
                if (stream.CanSeek)
                {
                    stream.Seek(default, SeekOrigin.Begin);
                }

                await _cipher.EncryptAsync(stream, tmp, _settings.CipherChunkSizeBytes, ct).ConfigureAwait(false);
                await tmp.FlushAsync(ct).ConfigureAwait(false);
                tmp.Flush(true);
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
        }


        public Stream GetBlobStream(string[] uids)
        {
            ArgumentNullException.ThrowIfNull(uids);
            foreach (var uid in uids)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            }
            return new ConcatenatedReadStream(this, uids);
        }
    }
}
