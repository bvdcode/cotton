using Cotton.Database.Models.Enums;
using Cotton.Server.Infrastructure;
using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Dto
{
    public class InitialServerSettingsRequestDto
    {
        /// <summary>
        /// Gets or sets the trusted mode flag which determines if the server can share chunks between users and use global indexing.
        /// </summary>
        public bool TrustedMode { get; init; }

        [JsonConverter(typeof(JsonEnumArrayConverter<ServerUsage>))]
        public ServerUsage[] Usage { get; init; } = [];

        public bool Telemetry { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StorageType Storage { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EmailMode Email { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ComputionMode ComputionMode { get; init; }

        public string Timezone { get; init; } = null!;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StorageSpaceMode StorageSpace { get; init; }

        public GeoIpLookupMode GeoIpLookupMode { get; init; }

        public string? PublicBaseUrl { get; set; }

        public S3Config? S3Config { get; init; }

        public EmailConfig? EmailConfig { get; init; }

        public string? CustomGeoIpLookupUrl { get; set; }
    }
}
