using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Serialization;

namespace Cotton.Server.Helpers
{
    public static class GeoIpHelpers
    {
        private const int MaxTTLDays = 7;
        private const int CacheSizeLimit = 10240;
        private const string url = "https://geoip.splidex.com/";
        private static readonly MemoryCache _cache = new(new MemoryCacheOptions
        {
            SizeLimit = CacheSizeLimit
        });

        public static async Task<GeoIpInfo> LookupAsync(string ip)
        {
            if (_cache.TryGetValue(ip, out GeoIpInfo? cachedInfo) && cachedInfo != null)
            {
                return cachedInfo;
            }
            using var httpClient = new HttpClient();
            var response = httpClient.GetAsync(url + ip).Result;
            if (!response.IsSuccessStatusCode)
            {
                return new GeoIpInfo
                {
                    Country = "Unknown",
                    Region = "Unknown",
                    City = "Unknown"
                };
            }
            try
            {
                var geoIpData = await response.Content.ReadFromJsonAsync<GeoIpInfo>();
                if (geoIpData != null)
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSize(1)
                        .SetSlidingExpiration(TimeSpan.FromDays(MaxTTLDays));
                    _cache.Set(ip, geoIpData, cacheEntryOptions);
                }
                return geoIpData ?? new GeoIpInfo
                {
                    Country = "Unknown",
                    Region = "Unknown",
                    City = "Unknown"
                };
            }
            catch
            {
                return new GeoIpInfo
                {
                    Country = "Unknown",
                    Region = "Unknown",
                    City = "Unknown"
                };
            }
        }
    }

    public record GeoIpInfo
    {
        public string Country { get; init; } = "Unknown";

        [JsonPropertyName("stateprov")]
        public string Region { get; init; } = "Unknown";
        public string City { get; init; } = "Unknown";
    }
}
