// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Describes s3 configuration.
    /// </summary>
    public class S3Config
    {
        /// <summary>
        /// Gets or sets access key.
        /// </summary>
        public string AccessKey { get; init; } = null!;
        /// <summary>
        /// Gets or sets secret key.
        /// </summary>
        public string SecretKey { get; init; } = null!;
        /// <summary>
        /// Gets or sets endpoint.
        /// </summary>
        public string Endpoint { get; init; } = null!;
        /// <summary>
        /// Gets or sets region.
        /// </summary>
        public string Region { get; init; } = null!;
        /// <summary>
        /// Gets or sets bucket.
        /// </summary>
        public string Bucket { get; init; } = null!;
    }
}
