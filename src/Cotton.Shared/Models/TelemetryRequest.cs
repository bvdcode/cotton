using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Models
{
    /// <summary>
    /// Represents a telemetry request containing the instance identifier, server URL, and associated JSON content.
    /// </summary>
    public class TelemetryRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this instance.
        /// </summary>
        public Guid InstanceId { get; set; }

        /// <summary>
        /// Gets or sets the URL of the server to which the application connects.
        /// </summary>
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of users associated with the current instance.
        /// </summary>
        public int NumberOfUsers { get; set; }

        public 
    }
}
