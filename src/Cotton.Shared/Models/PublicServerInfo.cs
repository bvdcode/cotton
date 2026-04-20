using System;

namespace Cotton.Models
{
    /// <summary>
    /// Represents publicly available information about the Cotton Cloud server.
    /// </summary>
    public class PublicServerInfo
    {
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string Product { get; set; } = null!;

        /// <summary>
        /// Gets or sets the hashed instance identifier.
        /// </summary>
        [Obsolete("This property is deprecated and should not be used. It may be removed in future versions.")]
        public string InstanceIdHash { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the initial administrator can be created.
        /// </summary>
        /// <remarks>This property is typically used during the setup process to determine if the
        /// application allows the creation of an initial admin user.</remarks>
        public bool CanCreateInitialAdmin { get; set; }
    }
}
