using System;

namespace Cotton.Models
{
    /// <summary>
    /// Represents publicly available information about the Cotton Cloud server.
    /// </summary>
    public class PublicServerInfo
    {
        /// <summary>
        /// Gets or sets the current server time.
        /// </summary>
        public DateTime CurrentTime { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string Product { get; set; } = null!;

        /// <summary>
        /// Gets or sets the version number.
        /// </summary>
        public string Version { get; set; } = null!;

        /// <summary>
        /// Gets or sets the hashed instance identifier.
        /// </summary>
        public string InstanceIdHash { get; set; } = null!;
    }
}
