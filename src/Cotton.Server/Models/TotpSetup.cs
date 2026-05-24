// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    /// <summary>
    /// Describes totp setup.
    /// </summary>
    public class TotpSetup
    {
        /// <summary>
        /// Gets or sets the secret base32.
        /// </summary>
        public string SecretBase32 { get; set; } = null!;
        /// <summary>
        /// Gets or sets the otp auth URI.
        /// </summary>
        public string OtpAuthUri { get; set; } = null!;
    }
}
