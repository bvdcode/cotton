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

        public FileStorage(CottonSettings settings, IStreamCipher cipher)
        {
            _cipher = cipher;
            _settings = settings;
            _basePath = Path.Combine(AppContext.BaseDirectory, "chunks");
            Directory.CreateDirectory(_basePath);
        }

        public async Task WriteChunkAsync(string hash, Stream stream)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash required", nameof(hash));
            }
            ArgumentNullException.ThrowIfNull(stream);
            string p1 = hash[..2];
            string p2 = hash[2..4];
            string dirPath = Path.Combine(_basePath, p1, p2);
            Directory.CreateDirectory(dirPath);
            string filePath = Path.Combine(dirPath, hash[4..]);
            if (File.Exists(filePath))
            {
                // TODO: Read and verify existing file integrity
            }
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            if (stream.CanSeek)
            {
                stream.Seek(default, SeekOrigin.Begin);
            }
            await _cipher.EncryptAsync(stream, fileStream, _settings.CipherChunkSizeBytes);
            await fileStream.FlushAsync();
        }
    }
}
