using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using EasyExtensions.Clients;
using System.Net;

namespace Cotton.Server.Services
{
    public sealed class GeoLookupService(SettingsProvider _settings) : IGeoLookupService
    {
        public async Task<GeoLookupResult?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            // TODO: Hotfix - just use cloud resolver when telemetry is opt in

            var settings = _settings.GetServerSettings();
            if (!settings.TelemetryEnabled)
            {
                return null;
            }
            ArgumentNullException.ThrowIfNull(ipAddress);
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
    }
}
