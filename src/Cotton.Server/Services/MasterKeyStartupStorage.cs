// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Database.Models.Enums;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Helpers;
using Cotton.Crypto;
using EasyExtensions.Extensions;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
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
            StartupStorageSettings settings = LoadLatestStorageSettings(connectionString);

            return settings.StorageType switch
            {
                StorageType.Local => new FileSystemStorageBackend(
                    loggerFactory.CreateLogger<FileSystemStorageBackend>()),
                StorageType.S3 => new S3StorageBackend(new StaticS3Provider(settings, encryptionSettings)),
                _ => throw new NotSupportedException($"Storage type {settings.StorageType} is not supported.")
            };
        }

        private static StartupStorageSettings LoadLatestStorageSettings(string connectionString)
        {
            const string sql = """
            select
                storage_type,
                s3_endpoint_url,
                s3_region,
                s3_access_key_id,
                s3_secret_access_key_encrypted,
                s3_bucket_name
            from public.server_settings
            order by created_at desc
            limit 1;
            """;

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(sql, connection);
            try
            {
                using NpgsqlDataReader reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return StartupStorageSettings.Local;
                }

                return new StartupStorageSettings(
                    (StorageType)reader.GetInt32(0),
                    GetNullableString(reader, 1),
                    GetNullableString(reader, 2),
                    GetNullableString(reader, 3),
                    GetNullableString(reader, 4),
                    GetNullableString(reader, 5));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return StartupStorageSettings.Local;
            }
        }

        private static string? GetNullableString(NpgsqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private sealed record StartupStorageSettings(
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

        private sealed class StaticS3Provider : IS3Provider
        {
            private readonly string _endpointUrl;
            private readonly string _region;
            private readonly string _accessKeyId;
            private readonly string _secretAccessKeyEncrypted;
            private readonly string _bucketName;
            private readonly CottonEncryptionSettings _encryptionSettings;
            private IAmazonS3? _client;

            public StaticS3Provider(StartupStorageSettings settings, CottonEncryptionSettings encryptionSettings)
            {
                _endpointUrl = RequireConfigured(settings.S3EndpointUrl, nameof(settings.S3EndpointUrl));
                _region = RequireConfigured(settings.S3Region, nameof(settings.S3Region));
                _accessKeyId = RequireConfigured(settings.S3AccessKeyId, nameof(settings.S3AccessKeyId));
                _secretAccessKeyEncrypted = RequireConfigured(
                    settings.S3SecretAccessKeyEncrypted,
                    nameof(settings.S3SecretAccessKeyEncrypted));
                _bucketName = RequireConfigured(settings.S3BucketName, nameof(settings.S3BucketName));
                _encryptionSettings = encryptionSettings;
            }

            public string GetBucketName() => _bucketName;

            public IAmazonS3 GetS3Client()
            {
                return _client ??= S3CompatibilityFactory.BuildClient(
                    _endpointUrl,
                    _region,
                    _accessKeyId,
                    DecryptSecretAccessKey());
            }

            private string DecryptSecretAccessKey()
            {
                try
                {
                    byte[] encrypted = Convert.FromBase64String(_secretAccessKeyEncrypted);
                    using AesGcmStreamCipher cipher = MasterKeySentinelStore.CreateCipher(_encryptionSettings);
                    return cipher.DecryptString(encrypted);
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
