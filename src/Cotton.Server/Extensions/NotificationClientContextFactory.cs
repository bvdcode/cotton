// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using System.Net;

namespace Cotton.Server.Extensions
{
    internal static class NotificationClientContextFactory
    {
        private const string UnknownGeoLabel = "Unknown";
        private const string UnknownLocationLabel = "unknown location";
        private const string LocalNetworkLocationLabel = "local network";

        public static async Task<NotificationClientContext> CreateAsync(
            IGeoLookupService geoLookup,
            IPAddress ipAddress,
            StringValues userAgent)
        {
            string ip = ipAddress.ToString();
            UserAgentDeviceInfo device = UserAgentHelpers.GetDeviceInfo(userAgent);
            string deviceName = device.FriendlyName ?? device.Type.ToString();
            bool isLocalNetwork = NetworkAddressClassifier.IsLocalNetworkAddress(ipAddress);
            GeoLookupResult? ipInfo = isLocalNetwork
                ? null
                : await geoLookup.TryLookupAsync(ipAddress);

            return new NotificationClientContext(
                ip,
                userAgent.ToString(),
                deviceName,
                HasKnownDevice(deviceName),
                isLocalNetwork ? LocalNetworkLocationLabel : FormatGeoLocation(ipInfo),
                NormalizeGeoField(ipInfo?.Country),
                NormalizeGeoField(ipInfo?.Region),
                NormalizeGeoField(ipInfo?.City));
        }

        public static Dictionary<string, string> CreateMetadata(NotificationClientContext context)
        {
            return new Dictionary<string, string>
            {
                ["ip"] = context.Ip,
                ["userAgent"] = context.UserAgent,
                ["device"] = context.DeviceName,
                ["location"] = context.Location,
                ["country"] = context.Country,
                ["region"] = context.Region,
                ["city"] = context.City,
            };
        }

        public static string FormatInvariant(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool HasKnownDevice(string deviceName)
        {
            return !string.IsNullOrWhiteSpace(deviceName)
                && !string.Equals(deviceName, UnknownGeoLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeGeoField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? UnknownGeoLabel : value;
        }

        private static string FormatGeoLocation(GeoLookupResult? ipInfo)
        {
            if (ipInfo is null)
            {
                return UnknownLocationLabel;
            }

            string[] parts = new[] { ipInfo.City, ipInfo.Region, ipInfo.Country }
                .Where(IsKnownGeoField)
                .Select(value => value!.Trim())
                .ToArray();
            return parts.Length == 0 ? UnknownLocationLabel : string.Join(", ", parts);
        }

        private static bool IsKnownGeoField(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value.Trim(), UnknownGeoLabel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
