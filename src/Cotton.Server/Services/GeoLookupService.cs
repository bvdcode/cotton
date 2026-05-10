using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using EasyExtensions.Clients;
using System.Text.Json;
using System.Net;

namespace Cotton.Server.Services
{
    public sealed class GeoLookupService(SettingsProvider _settings) : IGeoLookupService
    {
        public async Task<GeoLookupResult?> TryLookupAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            var settings = _settings.GetServerSettings();
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (settings.GeoIpLookupMode == GeoIpLookupMode.Disabled)
            {
                return null;
            }

            if (settings.GeoIpLookupMode == GeoIpLookupMode.CustomHttp)
            {
                return await TryLookupWithCustomHttpAsync(settings.CustomGeoIpLookupUrl, ipAddress, cancellationToken);
            }

            if (settings.GeoIpLookupMode != GeoIpLookupMode.CottonCloud ||
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
                var json = await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);
                if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                var match = FindGeoFields(json);
                var country = match.Country;
                var region = match.Region;
                var city = match.City;

                if (country is null && region is null && city is null)
                {
                    return null;
                }

                return new GeoLookupResult(country, region, city);
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

        private static GeoFieldMatch FindGeoFields(JsonElement element)
        {
            var match = new GeoFieldMatch();
            FillGeoFields(element, match);
            return match;
        }

        private static void FillGeoFields(JsonElement element, GeoFieldMatch match)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String || property.Value.ValueKind == JsonValueKind.Number)
                    {
                        TryAssignGeoField(property.Name, property.Value, match);
                    }

                    FillGeoFields(property.Value, match);

                    if (match.IsComplete)
                    {
                        return;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    FillGeoFields(item, match);

                    if (match.IsComplete)
                    {
                        return;
                    }
                }
            }
        }

        private static void TryAssignGeoField(string propertyName, JsonElement value, GeoFieldMatch match)
        {
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                return;
            }

            var name = propertyName.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);

            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (match.Country is null && ContainsAny(name, "country", "countryname", "nation", "nationality", "countrycode"))
            {
                match.Country = text;
            }
            else if (match.Region is null && ContainsAny(name, "region", "state", "province", "county", "district", "territory", "prefecture"))
            {
                match.Region = text;
            }
            else if (match.City is null && ContainsAny(name, "city", "town", "locality", "village", "municipality"))
            {
                match.City = text;
            }
        }

        private static bool ContainsAny(string source, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (source.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class GeoFieldMatch
        {
            public string? Country { get; set; }
            public string? Region { get; set; }
            public string? City { get; set; }

            public bool IsComplete => Country is not null && Region is not null && City is not null;
        }
    }
}
