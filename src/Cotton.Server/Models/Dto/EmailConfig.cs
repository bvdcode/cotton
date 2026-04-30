namespace Cotton.Server.Models.Dto
{
    public class EmailConfig
    {
        public string Username { get; init; } = null!;
        public string Password { get; init; } = null!;
        public string SmtpServer { get; init; } = null!;
        public string Port { get; init; } = null!;
        public string FromAddress { get; init; } = null!;
        public bool UseSSL { get; init; }
    }
}
