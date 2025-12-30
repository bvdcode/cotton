using Cotton.Database.Models.Enums;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;

namespace Cotton.Server.Providers
{
    public class StorageBackendProvider(
        SettingsProvider _settings,
        IServiceProvider _serviceProvider) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend()
        {
            StorageType type = _settings.GetServerSettings().StorageType;
            if (type == StorageType.S3)
            {
                return ActivatorUtilities.CreateInstance<S3StorageBackend>(_serviceProvider);
            }
            if (type == StorageType.Local)
            {
                return ActivatorUtilities.CreateInstance<FileSystemStorageBackend>(_serviceProvider);
            }
            throw new NotSupportedException($"Storage type {type} is not supported.");
        }
    }
}
