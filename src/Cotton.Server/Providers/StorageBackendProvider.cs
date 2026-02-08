using Cotton.Database.Models.Enums;
using System.Threading;

namespace Cotton.Server.Providers
{
    public interface IStorageBackendTypeCache
    {
        StorageType Get();
        void Set(StorageType type);
        bool TryGet(out StorageType type);
        void Reset();
    }

    public sealed class StorageBackendTypeCache : IStorageBackendTypeCache
    {
        private int _hasValue;
        private StorageType _value;

        public StorageType Get()
        {
            return TryGet(out var type)
                ? type
                : throw new InvalidOperationException("Storage backend type cache is not initialized.");
        }

        public bool TryGet(out StorageType type)
        {
            if (Volatile.Read(ref _hasValue) == 1)
            {
                type = _value;
                return true;
            }

            type = default;
            return false;
        }

        public void Set(StorageType type)
        {
            _value = type;
            Volatile.Write(ref _hasValue, 1);
        }

        public void Reset()
        {
            Volatile.Write(ref _hasValue, 0);
            _value = default;
        }
    }

    public class StorageBackendProvider(
        IStorageBackendTypeCache _storageTypeCache,
        SettingsProvider _settings,
        IServiceProvider _serviceProvider) : global::Cotton.Storage.Abstractions.IStorageBackendProvider
    {
        public global::Cotton.Storage.Abstractions.IStorageBackend GetBackend()
        {
            if (!_storageTypeCache.TryGet(out var type))
            {
                type = _settings.GetServerSettings().StorageType;
                _storageTypeCache.Set(type);
            }
            if (type == StorageType.S3)
            {
                return ActivatorUtilities.CreateInstance<global::Cotton.Storage.Backends.S3StorageBackend>(_serviceProvider);
            }
            if (type == StorageType.Local)
            {
                return ActivatorUtilities.CreateInstance<global::Cotton.Storage.Backends.FileSystemStorageBackend>(_serviceProvider);
            }
            throw new NotSupportedException($"Storage type {type} is not supported.");
        }
    }
}
