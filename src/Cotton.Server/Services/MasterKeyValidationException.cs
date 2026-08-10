// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Indicates that a candidate master key could not be validated against existing Cotton data.
    /// </summary>
    internal class MasterKeyValidationException : Exception
    {
        /// <summary>
        /// Initializes a master-key validation exception.
        /// </summary>
        public MasterKeyValidationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a master-key validation exception with its underlying cause.
        /// </summary>
        public MasterKeyValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
