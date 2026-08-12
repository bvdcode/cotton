// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class MasterKeyValidationException : Exception
    {
        public MasterKeyValidationException(string message)
            : base(message)
        {
        }

        public MasterKeyValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
