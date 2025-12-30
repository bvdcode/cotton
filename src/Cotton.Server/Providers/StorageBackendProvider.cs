using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;

namespace Cotton.Server.Providers
{
    public class StorageBackendProvider(IServiceProvider _serviceProvider) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend()
        {
            return ActivatorUtilities.CreateInstance<FileSystemStorageBackend>(_serviceProvider);
        }
    }
}
