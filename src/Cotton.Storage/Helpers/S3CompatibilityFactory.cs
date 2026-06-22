// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Cotton.Storage.Helpers
{
    /// <summary>
    /// Builds S3-compatible clients and requests with Cotton's provider compatibility defaults.
    /// </summary>
    public static class S3CompatibilityFactory
    {
        /// <summary>Builds an Amazon S3 config suitable for path-style S3-compatible providers.</summary>
        public static AmazonS3Config BuildConfig(
            string endpoint,
            string region,
            TimeSpan? timeout = null,
            int maxErrorRetry = 3)
        {
            return new AmazonS3Config
            {
                UseHttp = false,
                ServiceURL = endpoint,
                AuthenticationRegion = region,
                ForcePathStyle = true,
                MaxErrorRetry = maxErrorRetry,
                Timeout = timeout ?? TimeSpan.FromMinutes(5),
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
        }

        /// <summary>Builds an S3 client from explicit endpoint credentials.</summary>
        public static AmazonS3Client BuildClient(
            string endpoint,
            string region,
            string accessKey,
            string secretKey,
            TimeSpan? timeout = null,
            int maxErrorRetry = 3)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            AmazonS3Config config = BuildConfig(endpoint, region, timeout, maxErrorRetry);
            return new AmazonS3Client(credentials, config);
        }

        /// <summary>Disables chunked transfer encoding for file-backed uploads.</summary>
        public static PutObjectRequest WithFileBodyCompatibility(this PutObjectRequest request)
        {
            request.UseChunkEncoding = false;
            return request;
        }

        /// <summary>Disables request features that are not accepted by some S3-compatible providers for memory-backed uploads.</summary>
        public static PutObjectRequest WithInMemoryBodyCompatibility(this PutObjectRequest request)
        {
            request.UseChunkEncoding = false;
            request.DisablePayloadSigning = true;
            return request;
        }
    }
}
