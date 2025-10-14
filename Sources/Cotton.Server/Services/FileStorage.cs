using System.IO.Pipelines;
using Cotton.Server.Streams;
using Cotton.Server.Settings;
using Cotton.Crypto.Abstractions;
using Cotton.Server.Abstractions;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services
{
    public partial class FileStorage : IStorage
    {
        private readonly string _basePath;
        private readonly IStreamCipher _cipher;
        private readonly CottonSettings _settings;
        private readonly ILogger<FileStorage> _logger;
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "chunks";

        [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.Compiled)]
        private static partial Regex CreateHexSha256Regex();

        public FileStorage(CottonSettings settings, IStreamCipher cipher, ILogger<FileStorage> logger)
        {
            _logger = logger;
            _cipher = cipher;
            _settings = settings;
            _basePath = Path.Combine(AppContext.BaseDirectory, BaseDirectoryName);
            Directory.CreateDirectory(_basePath);
        }

        public async Task<Stream> GetChunkReadStream(string hash, CancellationToken ct = default)
        {
            hash = NormalizeHash(hash);
            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..] + ChunkFileExtension);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Chunk not found", filePath);
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

        public async Task WriteChunkAsync(string hash, Stream stream, CancellationToken ct = default)
        {
            hash = NormalizeHash(hash);
            ArgumentNullException.ThrowIfNull(stream);

            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..] + ChunkFileExtension);
            if (File.Exists(filePath))
            {
                _logger.LogCritical("File collision detected for chunk {Hash}", hash);
                throw new IOException("File collision detected: two different chunks have the same name: " + hash);
            }

            string tmpFilePath = Path.Combine(dirPath, $"{hash[4..]}.{Guid.NewGuid():N}.tmp");
            var fso = new FileStreamOptions
            {
                Share = FileShare.None,
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };

            try
            {
                _logger.LogInformation("Storing new chunk {Hash}", hash);
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

        private string GetFolderByHash(string hash)
        {
            hash = NormalizeHash(hash);
            string p1 = hash[..2];
            string p2 = hash[2..4];
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

        internal Stream CreateDecryptingReadStream(string hash)
        {
            hash = NormalizeHash(hash);
            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..] + ChunkFileExtension);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Chunk not found", filePath);
            }

            var fso = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Access = FileAccess.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };

            var pipe = new Pipe();
            var readerStream = pipe.Reader.AsStream();
            var writerStream = pipe.Writer.AsStream();
            var fs = new FileStream(filePath, fso);

            _ = Task.Run(async () =>
            {
                Exception? error = null;
                try
                {
                    await _cipher.DecryptAsync(fs, writerStream).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    try
                    {
                        await writerStream.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispose writer stream");
                    }
                    try
                    {
                        await fs.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispose file stream");
                    }
                    pipe.Writer.Complete(error);
                }
            });

            return readerStream;
        }

        public Stream GetBlobStream(string[] hashes)
        {
            ArgumentNullException.ThrowIfNull(hashes);
            foreach (var hash in hashes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(hash);
            }
            return new ConcatenatedReadStream(this, hashes);
        }

        private static string NormalizeHash(string hash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash);
            string normalized = hash.Trim().ToLowerInvariant();
            if (!CreateHexSha256Regex().IsMatch(normalized))
            {
                throw new ArgumentException("Invalid chunk hash.", nameof(hash));
            }
            return normalized;
        }
    }
}
