using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Dto
{
    public enum MultiUserMode
    {
        Unknown = 0,
        Family = 1,
        Many = 2,
    }

    public enum ServerUsage
    {
        Other = 0,
        Photos = 1,
        Documents = 2,
        Media = 3,
    }

    public class ServerSettingsRequestDto
    {
        /// <summary>
        /// Gets or sets the multiuser mode setting for the application.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MultiUserMode Multiuser { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ServerUsage[] Usage { get; set; } = [];

        public string Telemetry { get; set; } = null!;
        public string Storage { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Ai { get; set; } = null!;
        public string Timezone { get; set; } = null!;
        public string StorageSpace { get; set; } = null!;
        public string[] ImportSources { get; set; } = [];
        public S3Config S3Config { get; init; } = null!;
        public EmailConfig EmailConfig { get; init; } = null!;
        public RemoteServiceConfig NextcloudConfig { get; set; } = null!;
        public RemoteServiceConfig WebdavConfig { get; set; } = null!;
    }

    public class S3Config
    {
        public string AccessKey { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public string Endpoint { get; set; } = null!;
        public string Region { get; set; } = null!;
        public string Bucket { get; set; } = null!;
    }

    public class EmailConfig
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string SmtpServer { get; set; } = null!;
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
