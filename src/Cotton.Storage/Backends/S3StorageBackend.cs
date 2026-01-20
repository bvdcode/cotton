using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;
using Cotton.Storage.Streams;
using System.Net;
using System.Net.Mime;

namespace Cotton.Storage.Backends
{
    public class S3StorageBackend(IS3Provider _s3Provider) : IStorageBackend
    {
        private const int WriteBufferSize = 2 * 1024 * 1024;

        private static string GetS3Key(string uid)
        {
            var (p1, p2, fileName) = StorageKeyHelper.GetSegments(uid);
            return $"{p1}/{p2}/{fileName}.ctn";
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
            return new S3ResponseStream(result);
        }

        public async Task<bool> ExistsAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrEmpty(uid);
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);

            var req = new GetObjectMetadataRequest
            {
                Key = key,
                BucketName = bucket,
            };

            try
            {
                var res = await _s3.GetObjectMetadataAsync(req);
                return res.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task WriteAsync(string uid, Stream source)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            ArgumentNullException.ThrowIfNull(source);

            var s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            string key = GetS3Key(uid);
            if (await ExistsAsync(uid).ConfigureAwait(false))
            {
                return;
            }

            string tmpPath = Path.GetTempFileName();
            try
            {
                await using (var fs = new FileStream(
                    tmpPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: WriteBufferSize,
                    useAsync: true))
                {
                    if (source.CanSeek)
                    {
                        source.Seek(0, SeekOrigin.Begin);
                    }

                    await source.CopyToAsync(fs).ConfigureAwait(false);
                    await fs.FlushAsync().ConfigureAwait(false);
                }
                var req = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    FilePath = tmpPath,
                    ContentType = MediaTypeNames.Application.Octet,
                    UseChunkEncoding = false,
                };
                await s3.PutObjectAsync(req).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    File.Delete(tmpPath);
                }
                catch { }
            }
        }
    }
}
