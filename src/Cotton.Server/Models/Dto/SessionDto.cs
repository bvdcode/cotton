using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    public class SessionDto
    {
        public string IpAddress { get; set; } = null!;
        public string UserAgent { get; set; } = null!;
        public AuthType AuthType { get; set; }
        public string Country { get; set; } = null!;
        public string Region { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Device { get; set; } = null!;
        public string SessionId { get; set; } = null!;
        public int RefreshTokenCount { get; set; }
        public TimeSpan TotalSessionDuration { get; set; }
        public bool IsCurrentSession { get; set; }
        public DateTime LastSeenAt { get; set; }
    }
}
