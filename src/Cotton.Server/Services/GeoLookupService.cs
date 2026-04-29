using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using EasyExtensions.Clients;
using System.Net;
using System.Net.Http.Json;

namespace Cotton.Server.Services
{
    public sealed class GeoLookupService(SettingsProvider _settings) : IGeoLookupService
    {
        public async Task<GeoLookupResult?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            var settings = _settings.GetServerSettings();
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (settings.GeoIpLookupMode == Cotton.Database.Models.Enums.GeoIpLookupMode.Disabled)
            {
                return null;
            }

            if (settings.GeoIpLookupMode == Cotton.Database.Models.Enums.GeoIpLookupMode.CustomHttp)
            {
                return await TryLookupWithCustomHttpAsync(settings.CustomGeoIpLookupUrl, ipAddress, cancellationToken);
            }

            if (settings.GeoIpLookupMode != Cotton.Database.Models.Enums.GeoIpLookupMode.CottonCloud ||
                !settings.TelemetryEnabled)
            {
                return null;
            }

            var geo = await GeoIpClient.TryLookupAsync(ipAddress.ToString(), cancellationToken);
            if (geo is null)
            {
                return null;
            }

            return new GeoLookupResult(
                Country: geo.Country,
                Region: geo.Region,
                City: geo.City);
        }

        private static async Task<GeoLookupResult?> TryLookupWithCustomHttpAsync(
            string? lookupUrl,
            IPAddress ipAddress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(lookupUrl))
            {
                return null;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string url = BuildCustomLookupUrl(lookupUrl, ipAddress);
                return await client.GetFromJsonAsync<GeoLookupResult>(url, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildCustomLookupUrl(string lookupUrl, IPAddress ipAddress)
        {
            string escapedIp = Uri.EscapeDataString(ipAddress.ToString());
            if (lookupUrl.Contains("{ip}", StringComparison.OrdinalIgnoreCase))
            {
                return lookupUrl.Replace("{ip}", escapedIp, StringComparison.OrdinalIgnoreCase);
            }

            char separator = lookupUrl.Contains('?') ? '&' : '?';
            return $"{lookupUrl}{separator}ip={escapedIp}";
        }
    }
}
