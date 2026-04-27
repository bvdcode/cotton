using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using EasyExtensions.Clients;
using EasyExtensions.Clients.Models;
using System.Net;

namespace Cotton.Server.Services
{
    public sealed class GeoLookupService(SettingsProvider _settings) : IGeoLookupService
    {
        public async Task<GeoIpInfo?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            // TODO: Hotfix - just use cloud resolver when telemetry is opt in

            var settings = _settings.GetServerSettings();
            if (!settings.TelemetryEnabled)
            {
                return null;
            }
            ArgumentNullException.ThrowIfNull(ipAddress);
            return await GeoIpClient.TryLookupAsync(ipAddress.ToString(), cancellationToken);
        }
    }
}
