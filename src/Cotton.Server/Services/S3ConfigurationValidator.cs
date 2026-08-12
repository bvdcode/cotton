// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Server.Models.Dto;
using Cotton.Storage.Helpers;
using System.Net;

namespace Cotton.Server.Services
{
    public class S3ConfigurationValidator(ILogger<S3ConfigurationValidator> _logger)
    {
        public async Task<string?> ValidateAsync(
            S3Config? configuration,
            CancellationToken cancellationToken = default)
        {
            string? shapeError = ValidateShape(configuration);
            if (shapeError is not null)
            {
                return shapeError;
            }

            try
            {
                await ValidateConnectivityAsync(configuration!, cancellationToken);
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        internal static string? ValidateShape(S3Config? configuration)
        {
            if (configuration is null)
            {
                return "S3 settings must be provided.";
            }

            if (string.IsNullOrWhiteSpace(configuration.Endpoint))
            {
                return "S3 endpoint URL must be provided.";
            }

            if (!Uri.TryCreate(configuration.Endpoint, UriKind.Absolute, out Uri? endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                return "S3 endpoint URL must be an absolute HTTP or HTTPS URL.";
            }

            if (string.IsNullOrWhiteSpace(configuration.Region))
            {
                return "S3 region must be provided.";
            }

            if (string.IsNullOrWhiteSpace(configuration.Bucket))
            {
                return "S3 bucket must be provided.";
            }

            if (string.IsNullOrWhiteSpace(configuration.AccessKey))
            {
                return "S3 access key must be provided.";
            }

            return string.IsNullOrWhiteSpace(configuration.SecretKey)
                ? "S3 secret key must be provided."
                : null;
        }

        private async Task ValidateConnectivityAsync(
            S3Config configuration,
            CancellationToken cancellationToken)
        {
            using AmazonS3Client s3 = S3CompatibilityFactory.BuildClient(
                configuration.Endpoint,
                configuration.Region,
                configuration.AccessKey,
                configuration.SecretKey,
                timeout: TimeSpan.FromSeconds(30),
                maxErrorRetry: 5);

            string testKey = "cotton_server_test_object_" + Guid.NewGuid().ToString("N");
            Exception? validationFailure = null;
            bool testObjectCreated = false;
            try
            {
                await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = configuration.Bucket,
                    Key = testKey,
                    ContentBody = "test"
                }.WithInMemoryBodyCompatibility(), cancellationToken);
                testObjectCreated = true;

                using (GetObjectResponse getResponse = await s3.GetObjectAsync(
                    configuration.Bucket,
                    testKey,
                    cancellationToken))
                using (StreamReader reader = new(getResponse.ResponseStream))
                {
                    string content = await reader.ReadToEndAsync(cancellationToken);
                    if (content != "test")
                    {
                        throw new InvalidOperationException("S3 read access validation failed: content mismatch.");
                    }
                }

                ListObjectsV2Response listResponse = await s3.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = configuration.Bucket,
                    MaxKeys = 1
                }, cancellationToken);
                if (listResponse.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException("S3 list access validation failed: " + listResponse.HttpStatusCode);
                }

                if (listResponse.KeyCount <= 0)
                {
                    throw new InvalidOperationException("S3 list access validation failed: bucket is empty or inaccessible.");
                }
            }
            catch (Exception ex)
            {
                validationFailure = ex;
                throw;
            }
            finally
            {
                if (testObjectCreated)
                {
                    await DeleteTestObjectAsync(s3, configuration.Bucket, testKey, validationFailure);
                }
            }
        }

        private async Task DeleteTestObjectAsync(
            AmazonS3Client s3,
            string bucket,
            string testKey,
            Exception? validationFailure)
        {
            try
            {
                await s3.DeleteObjectAsync(bucket, testKey, CancellationToken.None);
            }
            catch (Exception cleanupFailure) when (validationFailure is not null)
            {
                _logger.LogWarning(
                    cleanupFailure,
                    "Failed to remove S3 validation object {TestKey} after validation failed.",
                    testKey);
            }
        }
    }
}
