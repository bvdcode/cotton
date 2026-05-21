// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;

namespace Cotton.Server.Providers
{
    public class S3Provider(SettingsProvider _settingsProvider) : IS3Provider
    {
        private IAmazonS3? _s3Client;
        private string? _bucketName;

        public string GetBucketName()
        {
            if (!string.IsNullOrEmpty(_bucketName))
            {
                return _bucketName;
            }
            string? result = _settingsProvider.GetServerSettings().S3BucketName;
            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidOperationException("S3 bucket name is not configured.");
            }
            _bucketName = result;
            return result;
        }

        public IAmazonS3 GetS3Client()
        {
            if (_s3Client != null)
            {
                return _s3Client;
            }

            var settings = _settingsProvider.GetServerSettings();
            ArgumentNullException.ThrowIfNull(settings.S3EndpointUrl, nameof(settings.S3EndpointUrl));
            ArgumentNullException.ThrowIfNull(settings.S3AccessKeyId, nameof(settings.S3AccessKeyId));
            ArgumentNullException.ThrowIfNull(settings.S3SecretAccessKeyEncrypted, nameof(settings.S3SecretAccessKeyEncrypted));
            ArgumentNullException.ThrowIfNull(settings.S3BucketName, nameof(settings.S3BucketName));
            ArgumentNullException.ThrowIfNull(settings.S3Region, nameof(settings.S3Region));

            string? secretAccessKey = settings.S3SecretAccessKeyEncrypted;
            if (string.IsNullOrEmpty(secretAccessKey))
            {
                throw new InvalidOperationException("S3 secret access key is not configured.");
            }

            _s3Client = S3CompatibilityFactory.BuildClient(
                settings.S3EndpointUrl,
                settings.S3Region,
                settings.S3AccessKeyId,
                secretAccessKey);
            return _s3Client;
        }
    }
}
