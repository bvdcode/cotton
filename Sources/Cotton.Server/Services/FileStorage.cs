using Cotton.Server.Settings;
using Cotton.Server.Abstractions;

namespace Cotton.Server.Services
{
    public class FileStorage(CottonSettings _settings) : IStorage
    {
        public Task WriteChunkAsync(string hash, Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
