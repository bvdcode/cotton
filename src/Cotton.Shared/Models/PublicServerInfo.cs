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

        /// <summary>
        /// Gets or sets the duration for which the system has been running since the last restart.
        /// </summary>
        /// <remarks>This property reflects the total uptime of the system, which can be useful for
        /// monitoring and diagnostics. It is represented as a TimeSpan value.</remarks>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server currently has any users connected.
        /// </summary>
        /// <remarks>Use this property to determine if user-related operations can be performed on the
        /// server. The value reflects the real-time presence of users and may change as users connect or
        /// disconnect.</remarks>
        public bool ServerHasUsers { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server has been initialized.
        /// </summary>
        /// <remarks>Check this property before performing operations that require the server to be
        /// initialized. Setting this property to <see langword="true"/> typically indicates that the server is ready to
        /// handle requests.</remarks>
        public bool IsServerInitialized { get; set; }
    }
}
