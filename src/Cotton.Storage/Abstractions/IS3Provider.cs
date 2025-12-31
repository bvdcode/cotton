using Amazon.S3;

namespace Cotton.Storage.Abstractions
{
    public interface IS3Provider
    {
        string GetBucketName();
        IAmazonS3 GetS3Client();
    }
}
