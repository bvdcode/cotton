// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using EasyExtensions.Clients;
using System.Text.Json;
using System.Net;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates geo lookup.
    /// </summary>
    public sealed class GeoLookupService(SettingsProvider _settings) : IGeoLookupService
    {
        private const string GoogleDnsIpAddress = "8.8.8.8";

        /// <summary>
        /// Attempts to lookup async.
        /// </summary>
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
                var attempt = await TryLookupWithCustomHttpAsync(settings.CustomGeoIpLookupUrl, ipAddress.ToString(), cancellationToken);
                return attempt.Result;
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

        /// <summary>
        /// Executes test custom lookup.
        /// </summary>
        public async Task<string?> TestCustomLookupAsync(string serverBaseUrl, CancellationToken cancellationToken = default)
        {
            var settings = _settings.GetServerSettings();
            string? lookupUrl = settings.CustomGeoIpLookupUrl;
            if (string.IsNullOrWhiteSpace(lookupUrl))
            {
                return "Custom GeoIP lookup URL must be configured before testing.";
            }

            var attempts = new[]
            {
                new CustomLookupTestInput(serverBaseUrl, "instance URL"),
                new CustomLookupTestInput(GoogleDnsIpAddress, "Google DNS IP"),
                new CustomLookupTestInput(string.Empty, "empty IP"),
            };

            var failureDetails = new List<string>(attempts.Length);
            foreach (var attemptInput in attempts)
            {
                var attempt = await TryLookupWithCustomHttpAsync(
                    lookupUrl,
                    attemptInput.Value,
                    cancellationToken);
                if (attempt.Result is not null)
                {
                    return null;
                }

                failureDetails.Add(
                    $"{attemptInput.Label}: {attempt.Error ?? "no geo fields in response"}");
            }

            return "Custom IP resolver test failed. Tried instance URL, Google DNS IP, and empty IP. "
                + string.Join("; ", failureDetails);
        }

        private static async Task<CustomLookupAttemptResult> TryLookupWithCustomHttpAsync(
            string? lookupUrl,
            string ipValue,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(lookupUrl))
            {
                return new CustomLookupAttemptResult(
                    Result: null,
                    Error: "lookup URL is empty");
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string url = BuildCustomLookupUrl(lookupUrl, ipValue);
                var json = await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);
                if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null)
                {
                    return new CustomLookupAttemptResult(
                        Result: null,
                        Error: "response body is empty");
                }

                var match = FindGeoFields(json);
                var country = match.Country;
                var region = match.Region;
                var city = match.City;

                if (country is null && region is null && city is null)
                {
                    return new CustomLookupAttemptResult(
                        Result: null,
                        Error: "response does not contain country, region, or city");
                }

                return new CustomLookupAttemptResult(
                    Result: new GeoLookupResult(country, region, city),
                    Error: null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new CustomLookupAttemptResult(
                    Result: null,
                    Error: "request timed out");
            }
            catch (Exception ex)
            {
                string error = string.IsNullOrWhiteSpace(ex.Message)
                    ? "request failed"
                    : ex.Message;
                return new CustomLookupAttemptResult(
                    Result: null,
                    Error: error);
            }
        }

        private static string BuildCustomLookupUrl(string lookupUrl, string ipValue)
        {
            string escapedIp = Uri.EscapeDataString(ipValue);
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
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    FillGeoFields(item, match);
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

            var countryPriority = GetCountryPriority(name);
            if (countryPriority > 0 && match.TrySetCountry(text, countryPriority))
            {
                return;
            }

            var regionPriority = GetRegionPriority(name);
            if (regionPriority > 0 && match.TrySetRegion(text, regionPriority))
            {
                return;
            }

            var cityPriority = GetCityPriority(name);
            if (cityPriority > 0)
            {
                match.TrySetCity(text, cityPriority);
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

        private static int GetCountryPriority(string name)
        {
            if (ContainsAny(name, "countrycode", "countryiso", "countryalpha", "iso2", "iso3"))
            {
                return 1;
            }

            if (ContainsAny(name, "countryname", "countryfullname", "nation", "nationality"))
            {
                return 2;
            }

            if (name.Equals("country", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 0;
        }

        private static int GetRegionPriority(string name)
        {
            if (ContainsAny(name, "region", "state", "province", "territory", "prefecture"))
            {
                return 2;
            }

            if (ContainsAny(name, "district"))
            {
                return 1;
            }

            return 0;
        }

        private static int GetCityPriority(string name)
        {
            if (ContainsAny(name, "city", "town", "locality", "village", "municipality"))
            {
                return 1;
            }

            return 0;
        }

        private sealed class GeoFieldMatch
        {
            /// <summary>
            /// Gets or sets the country.
            /// </summary>
            public string? Country { get; set; }
            /// <summary>
            /// Gets or sets the region.
            /// </summary>
            public string? Region { get; set; }
            /// <summary>
            /// Gets or sets the city.
            /// </summary>
            public string? City { get; set; }
            private int _countryPriority;
            private int _regionPriority;
            private int _cityPriority;

            /// <summary>
            /// Attempts to set country.
            /// </summary>
            public bool TrySetCountry(string value, int priority)
            {
                if (priority <= _countryPriority)
                {
                    return false;
                }

                Country = value;
                _countryPriority = priority;
                return true;
            }

            /// <summary>
            /// Attempts to set region.
            /// </summary>
            public bool TrySetRegion(string value, int priority)
            {
                if (priority <= _regionPriority)
                {
                    return false;
                }

                Region = value;
                _regionPriority = priority;
                return true;
            }

            /// <summary>
            /// Attempts to set city.
            /// </summary>
            public bool TrySetCity(string value, int priority)
            {
                if (priority <= _cityPriority)
                {
                    return false;
                }

                City = value;
                _cityPriority = priority;
                return true;
            }
        }

        private sealed record CustomLookupAttemptResult(
            GeoLookupResult? Result,
            string? Error);

        private sealed record CustomLookupTestInput(string Value, string Label);
    }
}
