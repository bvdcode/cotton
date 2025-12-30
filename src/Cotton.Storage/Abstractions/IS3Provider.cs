using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Storage.Abstractions
{
    public interface IS3Provider
    {
        string GetBucketName();
        IAmazonS3 GetS3Client();
    }
}
