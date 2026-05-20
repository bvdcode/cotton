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
        /// Gets or sets the stable public hash of this server instance identifier.
        /// </summary>
        /// <remarks>Relay and other public integrations use this fingerprint to recognize the
        /// Cotton instance without exposing the raw internal <c>InstanceId</c>. The value is not
        /// a secret and should remain stable for the lifetime of the instance.</remarks>
        public string InstanceIdHash { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the initial administrator can be created.
        /// </summary>
        /// <remarks>This property is typically used during the setup process to determine if the
        /// application allows the creation of an initial admin user.</remarks>
        public bool CanCreateInitialAdmin { get; set; }
    }
}
