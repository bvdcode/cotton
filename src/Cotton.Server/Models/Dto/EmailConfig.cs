// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Describes email configuration.
    /// </summary>
    public class EmailConfig
    {
        /// <summary>
        /// Gets the SMTP account username.
        /// </summary>
        public string Username { get; init; } = null!;
        /// <summary>
        /// Gets or sets the password submitted by the client.
        /// </summary>
        public string Password { get; init; } = null!;
        /// <summary>
        /// Gets or sets smtp server.
        /// </summary>
        public string SmtpServer { get; init; } = null!;
        /// <summary>
        /// Gets or sets port.
        /// </summary>
        public string Port { get; init; } = null!;
        /// <summary>
        /// Gets or sets from address.
        /// </summary>
        public string FromAddress { get; init; } = null!;
        /// <summary>
        /// Gets or sets use ssl.
        /// </summary>
        public bool UseSSL { get; init; }
    }
}
