using System.Buffers;
using Cotton.Server.Settings;
using Cotton.Server.Abstractions;
using Cotton.Crypto.Abstractions;

namespace Cotton.Server.Services
{
    public class FileStorage : IStorage
    {
        private readonly string _basePath;
        private readonly IStreamCipher _cipher;
        private readonly CottonSettings _settings;
        private readonly ILogger<FileStorage> _logger;

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

        public async Task WriteChunkAsync(string hash, Stream stream, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentException("Hash required", nameof(hash));
            ArgumentNullException.ThrowIfNull(stream);

            string p1 = hash[..2];
            string p2 = hash[2..4];
            string dirPath = Path.Combine(_basePath, p1, p2);
            Directory.CreateDirectory(dirPath);

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
                    stream.Seek(0, SeekOrigin.Begin);
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
