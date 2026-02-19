// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton
{
    /// <summary>
    /// Provides configuration settings for encryption operations, including key management and threading options.
    /// </summary>
    /// <remarks>Use this class to specify parameters required for secure data encryption. The Pepper property
    /// adds an additional layer of security to cryptographic operations. The MasterEncryptionKey and
    /// MasterEncryptionKeyId properties are used to identify and manage the primary encryption key. The
    /// EncryptionThreads property allows configuration of parallelism for encryption tasks. Ensure that sensitive
    /// values such as MasterEncryptionKey are stored securely and not exposed in logs or user interfaces.</remarks>
    public class CottonEncryptionSettings
    {
        /// <summary>
        /// Gets or sets the pepper seasoning used in the recipe.
        /// </summary>
        public string Pepper { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the master encryption key used to secure sensitive data within the application.
        /// </summary>
        /// <remarks>This key is required for encrypting and decrypting confidential information. Ensure
        /// that the key is stored securely and is not exposed in logs, error messages, or source control. Changing the
        /// key may render previously encrypted data inaccessible.</remarks>
        public string MasterEncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier for the master encryption key used in data encryption.
        /// </summary>
        /// <remarks>This property is essential for identifying the specific encryption key utilized for
        /// securing sensitive data. Ensure that the value is set correctly to maintain data integrity and
        /// security.</remarks>
        public int MasterEncryptionKeyId { get; set; }

        /// <summary>
        /// Gets or sets the number of threads used for encryption operations.
        /// </summary>
        /// <remarks>Increasing the number of encryption threads may improve performance for large data
        /// sets, but it can also increase resource usage. Consider the available system resources when configuring this
        /// property.</remarks>
        public int EncryptionThreads { get; set; }
    }
}
