using Cotton.Database.Models.Enums;
using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Dto
{
    public class ServerSettingsRequestDto
    {
        /// <summary>
        /// Gets or sets the trusted mode flag which determines if the server can share chunks between users and use global indexing.
        /// </summary>
        public bool TrustedMode { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
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

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImportSource[] ImportSources { get; init; } = [];

        public S3Config? S3Config { get; init; }

        public EmailConfig? EmailConfig { get; init; }

        public RemoteServiceConfig? WebdavConfig { get; init; }
    }

    public class S3Config
    {
        public string AccessKey { get; init; } = null!;
        public string SecretKey { get; init; } = null!;
        public string Endpoint { get; init; } = null!;
        public string Region { get; init; } = null!;
        public string Bucket { get; init; } = null!;
    }

    public class EmailConfig
    {
        public string Username { get; init; } = null!;
        public string Password { get; init; } = null!;
        public string SmtpServer { get; init; } = null!;
        public string Port { get; init; } = null!;
        public string FromAddress { get; init; } = null!;
        public bool UseSSL { get; init; }
    }

    public class RemoteServiceConfig
    {
        public string Username { get; init; } = null!;
        public string Password { get; init; } = null!;
        public string ServerUrl { get; init; } = null!;
    }
}
