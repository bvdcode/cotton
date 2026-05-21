// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;

namespace Cotton.Storage.Abstractions
{
    public interface IS3Provider
    {
        string GetBucketName();
        IAmazonS3 GetS3Client();
    }
}
