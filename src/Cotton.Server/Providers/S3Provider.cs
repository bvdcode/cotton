// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;

namespace Cotton.Server.Providers
{
    /// <summary>
    /// Provides s3 dependencies to server components.
    /// </summary>
    public class S3Provider : IS3Provider, IDisposable
    {
        private readonly Func<S3Config> _getConfiguration;
        private IAmazonS3? _s3Client;
        private string? _bucketName;

        /// <summary>
        /// Initializes an S3 provider from the persisted server settings.
        /// </summary>
        public S3Provider(SettingsProvider settingsProvider)
        {
            ArgumentNullException.ThrowIfNull(settingsProvider);
            _getConfiguration = () => CreateConfiguration(settingsProvider.GetServerSettings());
        }

        internal S3Provider(S3Config configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _getConfiguration = () => configuration;
        }

        /// <summary>
        /// Gets bucket name.
        /// </summary>
        public string GetBucketName()
        {
            if (!string.IsNullOrEmpty(_bucketName))
            {
                return _bucketName;
            }

            S3Config configuration = _getConfiguration();
            _bucketName = RequireConfigured(configuration.Bucket, "S3 bucket name");
            return _bucketName;
        }

        /// <summary>
        /// Gets s3 client.
        /// </summary>
        public IAmazonS3 GetS3Client()
        {
            if (_s3Client is not null)
            {
                return _s3Client;
            }

            S3Config configuration = _getConfiguration();
            string endpoint = RequireConfigured(configuration.Endpoint, "S3 endpoint URL");
            string region = RequireConfigured(configuration.Region, "S3 region");
            string accessKey = RequireConfigured(configuration.AccessKey, "S3 access key");
            string secretKey = RequireConfigured(configuration.SecretKey, "S3 secret access key");

            _s3Client = S3CompatibilityFactory.BuildClient(
                endpoint,
                region,
                accessKey,
                secretKey);
            return _s3Client;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _s3Client?.Dispose();
            GC.SuppressFinalize(this);
        }

        private static S3Config CreateConfiguration(ServerSettingsSnapshot settings)
        {
            return new S3Config
            {
                Endpoint = settings.S3EndpointUrl ?? string.Empty,
                Region = settings.S3Region ?? string.Empty,
                AccessKey = settings.S3AccessKeyId ?? string.Empty,
                SecretKey = settings.S3SecretAccessKey ?? string.Empty,
                Bucket = settings.S3BucketName ?? string.Empty
            };
        }

        private static string RequireConfigured(string? value, string settingName)
        {
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException($"{settingName} is not configured.");
        }
    }
}
