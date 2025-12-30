using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using System.Net;
using System.Net.Mime;

namespace Cotton.Storage.Backends
{
    public class S3StorageBackend(IS3Provider _s3Provider) : IStorageBackend
    {
        public async Task<bool> DeleteAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);            
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();            
            var response = await _s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                Key = uid,
                BucketName = bucket
            });
            return response.HttpStatusCode == HttpStatusCode.NoContent;
        }

        public async Task<Stream> ReadAsync(string uid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);            
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();            
            var result = await _s3.GetObjectAsync(new GetObjectRequest
            {
                Key = uid,
                BucketName = bucket,
                ChecksumMode = new ChecksumMode("DISABLED")
            });
            return result.ResponseStream;
        }

        public async Task WriteAsync(string uid, Stream stream)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uid);
            ArgumentNullException.ThrowIfNull(stream);
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
            string bucket = _s3Provider.GetBucketName();
            PutObjectRequest req = new()
            {
                Key = uid,
                InputStream = stream,
                BucketName = bucket,
                UseChunkEncoding = true,
                ContentType = MediaTypeNames.Application.Octet,
            };
            await _s3.PutObjectAsync(req);
        }
    }
}
