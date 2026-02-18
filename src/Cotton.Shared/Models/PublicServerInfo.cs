using System;

namespace Cotton.Models
{
    public class PublicServerInfo
    {
        public DateTime CurrentTime { get; set; }
        public string Product { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string InstanceIdHash { get; set; } = null!;
    }
}
