// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Cotton.Storage.Helpers
{
    public static class S3CompatibilityFactory
    {
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

        public static AmazonS3Client BuildClient(
            string endpoint,
            string region,
            string accessKey,
            string secretKey,
            TimeSpan? timeout = null,
            int maxErrorRetry = 3)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var config = BuildConfig(endpoint, region, timeout, maxErrorRetry);
            return new AmazonS3Client(credentials, config);
        }

        public static PutObjectRequest WithFileBodyCompatibility(this PutObjectRequest request)
        {
            request.UseChunkEncoding = false;
            return request;
        }

        public static PutObjectRequest WithInMemoryBodyCompatibility(this PutObjectRequest request)
        {
            request.UseChunkEncoding = false;
            request.DisablePayloadSigning = true;
            return request;
        }
    }
}
