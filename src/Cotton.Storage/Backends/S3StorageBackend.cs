using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;
using System.Net;
using System.Net.Mime;

namespace Cotton.Storage.Backends
{
    public class S3StorageBackend(IS3Provider _s3Provider) : IStorageBackend
    {
        private static string GetS3Key(string uid)
        {
            var (p1, p2, fileName) = StorageKeyHelper.GetSegments(uid);
            return $"{p1}/{p2}/{fileName}";
        }

        public async Task<bool> DeleteAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);

            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            var response = await _s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                Key = key,
                BucketName = bucket
            });
            return response.HttpStatusCode == HttpStatusCode.NoContent;
        }

        public async Task<Stream> ReadAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);

            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            var result = await _s3.GetObjectAsync(new GetObjectRequest
            {
                Key = key,
                BucketName = bucket,
                ChecksumMode = new ChecksumMode("DISABLED")
            });
            return result.ResponseStream;
        }

        public async Task WriteAsync(string uid, Stream stream)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            ArgumentNullException.ThrowIfNull(stream);

            Stream uploadStream = stream;
            MemoryStream? rewindBuffer = null;

            if (!uploadStream.CanSeek)
            {
                rewindBuffer = new MemoryStream();
                await uploadStream.CopyToAsync(rewindBuffer).ConfigureAwait(false);
                rewindBuffer.Position = 0;
                uploadStream = rewindBuffer;
            }
            else if (uploadStream.Position != 0)
            {
                uploadStream.Seek(default, SeekOrigin.Begin);
            }

            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            PutObjectRequest req = new()
            {
                Key = key,
                InputStream = uploadStream,
                BucketName = bucket,
                UseChunkEncoding = false,
                ContentType = MediaTypeNames.Application.Octet,
            };
            req.Headers.ContentLength = uploadStream.Length;
            await _s3.PutObjectAsync(req).ConfigureAwait(false);

            rewindBuffer?.Dispose();
        }
    }
}
