using Amazon.S3;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Helpers;
using EasyExtensions.Crypto;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

internal static class MasterKeyStartupStorage
{
    public static MasterKeySentinelStore CreateSentinelStore(
        CottonEncryptionSettings encryptionSettings,
        ILoggerFactory loggerFactory)
    {
        IStorageBackend storage = CreateConfiguredBackend(encryptionSettings, loggerFactory);
        var compatibilityProbe = new MasterKeyCompatibilityProbe(
            loggerFactory.CreateLogger<MasterKeyCompatibilityProbe>(),
            storage);

        return new MasterKeySentinelStore(
            loggerFactory.CreateLogger<MasterKeySentinelStore>(),
            storage,
            compatibilityProbe);
    }

    public static Task<bool> HasExistingCottonDataAsync(CancellationToken cancellationToken = default)
    {
        return MasterKeyCompatibilityProbe.HasExistingCottonDataAsync(cancellationToken: cancellationToken);
    }

    private static IStorageBackend CreateConfiguredBackend(
        CottonEncryptionSettings encryptionSettings,
        ILoggerFactory loggerFactory)
    {
        string connectionString = MasterKeyCompatibilityProbe.BuildConnectionStringFromEnvironment();
        var options = new DbContextOptionsBuilder<CottonDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using AesGcmStreamCipher cipher = MasterKeySentinelStore.CreateCipher(encryptionSettings);
        using var dbContext = new CottonDbContext(
            options,
            cipher,
            loggerFactory.CreateLogger<CottonDbContext>());
        var settingsProvider = new SettingsProvider(dbContext);
        CottonServerSettings settings = settingsProvider.GetServerSettings();

        return settings.StorageType switch
        {
            StorageType.Local => new FileSystemStorageBackend(
                loggerFactory.CreateLogger<FileSystemStorageBackend>()),
            StorageType.S3 => new S3StorageBackend(new StaticS3Provider(settings)),
            _ => throw new NotSupportedException($"Storage type {settings.StorageType} is not supported.")
        };
    }

    private sealed class StaticS3Provider : IS3Provider
    {
        private readonly string _endpointUrl;
        private readonly string _region;
        private readonly string _accessKeyId;
        private readonly string _secretAccessKey;
        private readonly string _bucketName;
        private IAmazonS3? _client;

        public StaticS3Provider(CottonServerSettings settings)
        {
            _endpointUrl = RequireConfigured(settings.S3EndpointUrl, nameof(settings.S3EndpointUrl));
            _region = RequireConfigured(settings.S3Region, nameof(settings.S3Region));
            _accessKeyId = RequireConfigured(settings.S3AccessKeyId, nameof(settings.S3AccessKeyId));
            _secretAccessKey = RequireConfigured(settings.S3SecretAccessKeyEncrypted, nameof(settings.S3SecretAccessKeyEncrypted));
            _bucketName = RequireConfigured(settings.S3BucketName, nameof(settings.S3BucketName));
        }

        public string GetBucketName() => _bucketName;

        public IAmazonS3 GetS3Client()
        {
            return _client ??= S3CompatibilityFactory.BuildClient(
                _endpointUrl,
                _region,
                _accessKeyId,
                _secretAccessKey);
        }

        private static string RequireConfigured(string? value, string settingName)
        {
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException($"{settingName} is not configured for S3 storage.");
        }
    }
}
