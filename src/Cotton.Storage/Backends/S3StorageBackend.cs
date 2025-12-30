using Amazon.S3;
using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Backends
{
    public class S3StorageBackend(IAmazonS3 _s3) : IStorageBackend
    {
        public Task<Stream> ReadAsync(string uid)
        {

        }

        public Task WriteAsync(string uid, Stream stream)
        {

        }
    }
}
