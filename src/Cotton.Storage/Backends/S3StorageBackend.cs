// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;
using Cotton.Storage.Streams;
using System.Net;
using System.Net.Mime;

namespace Cotton.Storage.Backends
{
    /// <summary>
    /// S3-compatible backend that stores opaque Cotton chunks as bucket objects.
    /// </summary>
    public class S3StorageBackend(IS3Provider _s3Provider) : IStorageBackend, IStorageBackendUsesEncryptedConfiguration
    {
        private const string InvalidRangeErrorCode = "InvalidRange";

        private static string GetS3Key(string uid)
        {
            var (p1, p2, fileName) = StorageKeyHelper.GetSegments(uid);
            return $"{p1}/{p2}/{fileName}.ctn";
        }

        public async Task<bool> DeleteAsync(string uid, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);

            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            DeleteObjectResponse response = await _s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                Key = key,
                BucketName = bucket
            }, cancellationToken);
            return response.HttpStatusCode == HttpStatusCode.NoContent;
        }

        public async Task<Stream> ReadAsync(string uid, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);
            GetObjectRequest request = new GetObjectRequest
            {
                Key = key,
                BucketName = bucket,
            }.WithFullContentReadCompatibility();

            try
            {
                GetObjectResponse result = await _s3.GetObjectAsync(request, cancellationToken);
                return new S3ResponseStream(result);
            }
            catch (AmazonS3Exception exception) when (
                exception.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
                && exception.ErrorCode == InvalidRangeErrorCode)
            {
                return new MemoryStream([], writable: false);
            }
        }

        public async Task<bool> ExistsAsync(string uid, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(uid);
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            GetObjectMetadataRequest req = new GetObjectMetadataRequest
            {
                Key = key,
                BucketName = bucket,
            };

            try
            {
                GetObjectMetadataResponse res = await _s3.GetObjectMetadataAsync(req, cancellationToken);
                return res.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<long> GetSizeAsync(string uid, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(uid);

            IAmazonS3 s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            GetObjectMetadataRequest req = new GetObjectMetadataRequest
            {
                Key = key,
                BucketName = bucket,
            };

            try
            {
                GetObjectMetadataResponse res = await s3.GetObjectMetadataAsync(req, cancellationToken).ConfigureAwait(false);
                return res.ContentLength;
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
            {
                return 0;
            }
        }

        public async Task<long> WriteAsync(string uid, Stream source, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            ArgumentNullException.ThrowIfNull(source);

            IAmazonS3 s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);
            if (!overwrite && await ExistsAsync(uid, cancellationToken).ConfigureAwait(false))
            {
                return await GetSizeAsync(uid, cancellationToken).ConfigureAwait(false);
            }

            await using PooledUploadStream body = await PooledUploadStream.CreateAsync(source, cancellationToken)
                .ConfigureAwait(false);
            PutObjectRequest request = new PutObjectRequest()
            {
                BucketName = bucket,
                Key = key,
                InputStream = body,
                AutoCloseStream = false,
                ContentType = MediaTypeNames.Application.Octet,
            }.WithFileBodyCompatibility();
            request.Headers.ContentLength = body.Length;
            await s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
            return body.Length;
        }

        public void CleanupTempFiles(TimeSpan ttl)
        {
            // No-op for S3 backend
        }

        public async IAsyncEnumerable<string> ListAllKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            IAmazonS3 s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string? continuationToken = null;

            do
            {
                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = bucket,
                    MaxKeys = 1000,
                    ContinuationToken = continuationToken
                };

                ListObjectsV2Response response = await s3.ListObjectsV2Async(request, ct);

                foreach (S3Object? obj in response.S3Objects)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!obj.Key.EndsWith(".ctn", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] parts = obj.Key.Split('/');
                    if (parts.Length != 3)
                    {
                        continue;
                    }

                    string p1 = parts[0];
                    string p2 = parts[1];
                    string fileName = Path.GetFileNameWithoutExtension(parts[2]);
                    string uid = p1 + p2 + fileName;

                    yield return uid;
                }

                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (continuationToken is not null);
        }

        public async IAsyncEnumerable<string> ListKeysByPrefixAsync(
            char prefix,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!Uri.IsHexDigit(prefix))
            {
                throw new ArgumentOutOfRangeException(nameof(prefix));
            }

            IAmazonS3 s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string? continuationToken = null;
            string keyPrefix = char.ToLowerInvariant(prefix).ToString();

            do
            {
                ListObjectsV2Response response = await s3.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = keyPrefix,
                    MaxKeys = 1000,
                    ContinuationToken = continuationToken,
                }, ct);

                foreach (S3Object obj in response.S3Objects)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!obj.Key.EndsWith(".ctn", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] parts = obj.Key.Split('/');
                    if (parts.Length == 3)
                    {
                        yield return parts[0] + parts[1] + Path.GetFileNameWithoutExtension(parts[2]);
                    }
                }

                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (continuationToken is not null);
        }
    }
}
