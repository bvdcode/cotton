// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the session API payload.
    /// </summary>
    public class SessionDto
    {
        /// <summary>
        /// Gets or sets ip address.
        /// </summary>
        public string IpAddress { get; set; } = null!;
        /// <summary>
        /// Gets or sets user agent.
        /// </summary>
        public string UserAgent { get; set; } = null!;
        /// <summary>
        /// Gets or sets auth type.
        /// </summary>
        public AuthType AuthType { get; set; }
        /// <summary>
        /// Gets or sets country.
        /// </summary>
        public string Country { get; set; } = null!;
        /// <summary>
        /// Gets or sets region.
        /// </summary>
        public string Region { get; set; } = null!;
        /// <summary>
        /// Gets or sets city.
        /// </summary>
        public string City { get; set; } = null!;
        /// <summary>
        /// Gets or sets device.
        /// </summary>
        public string Device { get; set; } = null!;
        /// <summary>
        /// Gets or sets session id.
        /// </summary>
        public string SessionId { get; set; } = null!;
        /// <summary>
        /// Gets or sets refresh token count.
        /// </summary>
        public int RefreshTokenCount { get; set; }
        /// <summary>
        /// Gets or sets total session duration.
        /// </summary>
        public TimeSpan TotalSessionDuration { get; set; }
        /// <summary>
        /// Indicates whether current session.
        /// </summary>
        public bool IsCurrentSession { get; set; }
        /// <summary>
        /// Gets or sets last seen at.
        /// </summary>
        public DateTime LastSeenAt { get; set; }
    }
}
