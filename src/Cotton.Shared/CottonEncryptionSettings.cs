// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton
{
    /// <summary>
    /// Provides configuration settings for encryption operations, including key management and threading options.
    /// </summary>
    public class CottonEncryptionSettings
    {
        /// <summary>
        /// Gets or sets the secret pepper appended to inputs before hashing.
        /// </summary>
        public string Pepper { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the master encryption key used to secure sensitive data within the application.
        /// </summary>
        public string MasterEncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier for the master encryption key used in data encryption.
        /// </summary>
        public int MasterEncryptionKeyId { get; set; }

        /// <summary>
        /// Gets or sets the number of threads used for encryption operations.
        /// </summary>
        public int EncryptionThreads { get; set; }
    }
}
