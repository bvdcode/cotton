using Cotton.Server.Settings;
using Cotton.Server.Abstractions;

namespace Cotton.Server.Services
{
    public class FileStorage : IStorage
    {
        private readonly string _basePath;
        private readonly CottonSettings _settings;

        public FileStorage(CottonSettings settings)
        {
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
            var path = Path.Combine(_basePath, hash);
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            long written = 0;
            byte[] buffer = new byte[128 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                written += read;
                if (written > _settings.ChunkSizeBytes)
                {
                    throw new InvalidOperationException($"Chunk exceeds configured size {_settings.ChunkSizeBytes} bytes");
                }
            }
        }
    }
}
