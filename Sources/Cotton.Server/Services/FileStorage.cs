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

        [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.Compiled)]
        private static partial Regex CreateHexHashRegex();
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "chunks";


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
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash required", nameof(hash));
            ArgumentNullException.ThrowIfNull(stream);

            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..] + ChunkFileExtension);
            if (File.Exists(filePath))
            {
                return;
            }

            string tmpFilePath = Path.Combine(dirPath, $"{hash[4..]}.{Guid.NewGuid():N}.tmp");

            var fso = new FileStreamOptions
            {
                Share = FileShare.None,
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };

            await using var tmp = new FileStream(tmpFilePath, fso);
            try
            {
                if (stream.CanSeek)
                {
                    stream.Seek(default, SeekOrigin.Begin);
                }

                await _cipher.EncryptAsync(stream, tmp, _settings.CipherChunkSizeBytes, ct).ConfigureAwait(false);
                await tmp.FlushAsync(ct).ConfigureAwait(false);
                tmp.Flush(true);
            }
            catch
            {
                TryDelete(tmpFilePath);
                throw;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    TryDelete(tmpFilePath);
                    return;
                }

                File.Move(tmpFilePath, filePath);
                File.SetAttributes(filePath, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
            }
            catch (IOException)
            {
                if (File.Exists(filePath))
                {
                    TryDelete(tmpFilePath);
                    return;
                }
                TryDelete(tmpFilePath);
                throw;
            }
            catch
            {
                TryDelete(tmpFilePath);
                throw;
            }
        }

        private string GetFolderByHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash) || !CreateHexHashRegex().IsMatch(hash))
            {
                throw new ArgumentException("Invalid chunk hash.", nameof(hash));
            }
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
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to delete temporary file {Path}", path);
            }
        }
    }
}
