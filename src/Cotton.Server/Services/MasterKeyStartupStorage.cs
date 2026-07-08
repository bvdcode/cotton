// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Database.Models.Enums;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Helpers;
using Cotton.Crypto;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services
{
    internal static class MasterKeyStartupStorage
    {
        public static async Task<MasterKeySentinelStore> CreateSentinelStoreAsync(
            CottonEncryptionSettings encryptionSettings,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken = default)
        {
            IStorageBackend storage = await CreateConfiguredBackendAsync(
                encryptionSettings,
                loggerFactory,
                cancellationToken);
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

        private static async Task<IStorageBackend> CreateConfiguredBackendAsync(
            CottonEncryptionSettings encryptionSettings,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            string connectionString = MasterKeyCompatibilityProbe.BuildConnectionStringFromEnvironment();
            StartupStorageSettings settings = await LoadLatestStorageSettingsAsync(connectionString, cancellationToken);

            return settings.StorageType switch
            {
                StorageType.Local => new FileSystemStorageBackend(
                    loggerFactory.CreateLogger<FileSystemStorageBackend>()),
                StorageType.S3 => new S3StorageBackend(await StaticS3Provider.CreateAsync(
                    settings,
                    encryptionSettings,
                    cancellationToken)),
                _ => throw new NotSupportedException($"Storage type {settings.StorageType} is not supported.")
            };
        }

        private static async Task<StartupStorageSettings> LoadLatestStorageSettingsAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            try
            {
                await using MasterKeyProbeDbContext dbContext = MasterKeyCompatibilityProbe.CreateDbContext(connectionString);
                StartupStorageSettings? settings = await dbContext.ServerSettings
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new StartupStorageSettings(
                        x.StorageType,
                        x.S3EndpointUrl,
                        x.S3Region,
                        x.S3AccessKeyId,
                        x.S3SecretAccessKeyEncrypted,
                        x.S3BucketName))
                    .FirstOrDefaultAsync(cancellationToken);
                return settings ?? StartupStorageSettings.Local;
            }
            catch (PostgresException ex) when (MasterKeyCompatibilityProbe.IsMissingDatabaseShape(ex))
            {
                return StartupStorageSettings.Local;
            }
        }

        private record StartupStorageSettings(
            StorageType StorageType,
            string? S3EndpointUrl,
            string? S3Region,
            string? S3AccessKeyId,
            string? S3SecretAccessKeyEncrypted,
            string? S3BucketName)
        {
            public static StartupStorageSettings Local { get; } = new(
                StorageType.Local,
                S3EndpointUrl: null,
                S3Region: null,
                S3AccessKeyId: null,
                S3SecretAccessKeyEncrypted: null,
                S3BucketName: null);
        }

        private class StaticS3Provider : IS3Provider
        {
            private readonly string _endpointUrl;
            private readonly string _region;
            private readonly string _accessKeyId;
            private readonly string _secretAccessKey;
            private readonly string _bucketName;
            private IAmazonS3? _client;

            private StaticS3Provider(
                StartupStorageSettings settings,
                string secretAccessKey)
            {
                _endpointUrl = RequireConfigured(settings.S3EndpointUrl, nameof(settings.S3EndpointUrl));
                _region = RequireConfigured(settings.S3Region, nameof(settings.S3Region));
                _accessKeyId = RequireConfigured(settings.S3AccessKeyId, nameof(settings.S3AccessKeyId));
                _secretAccessKey = secretAccessKey;
                _bucketName = RequireConfigured(settings.S3BucketName, nameof(settings.S3BucketName));
            }

            public static async Task<StaticS3Provider> CreateAsync(
                StartupStorageSettings settings,
                CottonEncryptionSettings encryptionSettings,
                CancellationToken cancellationToken)
            {
                string secretAccessKeyEncrypted = RequireConfigured(
                    settings.S3SecretAccessKeyEncrypted,
                    nameof(settings.S3SecretAccessKeyEncrypted));
                string secretAccessKey = await DecryptSecretAccessKeyAsync(
                    secretAccessKeyEncrypted,
                    encryptionSettings,
                    cancellationToken);
                return new StaticS3Provider(settings, secretAccessKey);
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

            private static async Task<string> DecryptSecretAccessKeyAsync(
                string secretAccessKeyEncrypted,
                CottonEncryptionSettings encryptionSettings,
                CancellationToken cancellationToken)
            {
                try
                {
                    byte[] encrypted = Convert.FromBase64String(secretAccessKeyEncrypted);
                    using AesGcmStreamCipher cipher = MasterKeySentinelStore.CreateCipher(encryptionSettings);
                    byte[] decrypted = await cipher.DecryptAsync(encrypted, cancellationToken);
                    return Encoding.UTF8.GetString(decrypted);
                }
                catch (Exception ex) when (ex is FormatException
                    or CryptographicException
                    or InvalidDataException)
                {
                    throw new InvalidOperationException(
                        "S3 secret access key could not be decrypted with the configured master key.",
                        ex);
                }
            }

            private static string RequireConfigured(string? value, string settingName)
            {
                return !string.IsNullOrWhiteSpace(value)
                    ? value
                    : throw new InvalidOperationException($"{settingName} is not configured for S3 storage.");
            }
        }
    }
}
