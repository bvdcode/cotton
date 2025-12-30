using Amazon.S3;
using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Backends
{
    public class S3StorageBackend(IS3Provider _s3Provider) : IStorageBackend
    {
        public Task<Stream> ReadAsync(string uid)
        {
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
        }

        public Task WriteAsync(string uid, Stream stream)
        {
            IAmazonS3 _s3 = _s3Provider.GetS3Client();
        }
    }
}
