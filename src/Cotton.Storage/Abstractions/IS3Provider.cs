// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Supplies the currently configured S3-compatible client and bucket name.
    /// </summary>
    public interface IS3Provider
    {
        /// <summary>Returns the target bucket name.</summary>
        string GetBucketName();
        /// <summary>Creates or returns an S3-compatible client for the active storage settings.</summary>
        IAmazonS3 GetS3Client();
    }
}
