// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the result of push device token revocation.
    /// </summary>
    public class PushDeviceTokenRevocationResultDto
    {
        /// <summary>
        /// Gets or sets the number of revoked tokens.
        /// </summary>
        public int RevokedTokens { get; set; }
    }
}
