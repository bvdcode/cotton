// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using EasyHttpRequestExtensions = EasyExtensions.AspNetCore.Extensions.HttpRequestExtensions;

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Resolves client addresses only after validating the immediate reverse proxy when one is configured.
    /// </summary>
    public static class TrustedProxyRequestExtensions
    {
        private const byte Ipv4PrefixLength = 32;
        private const byte Ipv6PrefixLength = 128;

        private static IPNetwork Private172Network { get; } = IPNetwork.Parse("172.16.0.0/12");

        /// <summary>
        /// Reserved settings value that selects direct-connection mode and disables forwarded client-address headers.
        /// </summary>
        internal static IPAddress DirectConnectionIpAddress { get; } = IPAddress.Any;

        /// <summary>
        /// Gets the client address according to the configured trust mode: direct connection, validated proxy, or
        /// legacy header trust while no trusted proxy has been configured.
        /// </summary>
        public static IPAddress GetTrustedClientIPAddress(this HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            SettingsProvider settingsProvider = request.HttpContext.RequestServices
                .GetRequiredService<SettingsProvider>();
            CottonServerSettings settings = settingsProvider.GetServerSettings();
            return request.GetTrustedClientIPAddress(
                settings.TrustedProxyIpAddress,
                settings.TrustedProxyPrefixLength);
        }

        internal static IPAddress GetTrustedClientIPAddress(
            this HttpRequest request,
            IPAddress? trustedProxyIpAddress,
            byte? trustedProxyPrefixLength = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (IsDirectConnectionMode(trustedProxyIpAddress, trustedProxyPrefixLength))
            {
                return request.GetConnectingIPAddress()
                    ?? throw new InvalidOperationException(
                        "The direct client connection IP address is unavailable for this request.");
            }

            if (trustedProxyIpAddress is not null)
            {
                IPAddress? connectingIpAddress = request.GetConnectingIPAddress();
                if (!MatchesTrustedProxy(
                        trustedProxyIpAddress,
                        trustedProxyPrefixLength,
                        connectingIpAddress))
                {
                    throw new UntrustedProxyConnectionException(
                        Normalize(trustedProxyIpAddress),
                        trustedProxyPrefixLength,
                        connectingIpAddress);
                }
            }

            // EasyExtensions currently resolves CF-Connecting-IP, then X-Real-IP, then X-Forwarded-For, and finally
            // Connection.RemoteIpAddress. The configured peer check above is the trust boundary around those headers.
            return EasyHttpRequestExtensions.GetRemoteIPAddress(request);
        }

        internal static bool IsDirectConnectionMode(
            IPAddress? address,
            byte? prefixLength = null)
        {
            return prefixLength is null
                && address is not null
                && Normalize(address).Equals(DirectConnectionIpAddress);
        }

        internal static IPAddress? GetConnectingIPAddress(this HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            IPAddress? address = request.HttpContext.Connection.RemoteIpAddress;
            return address is null ? null : Normalize(address);
        }

        internal static bool TryParseTrustedProxy(
            string value,
            out IPAddress address,
            out byte? prefixLength)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            if (IPAddress.TryParse(value, out IPAddress? exactAddress))
            {
                address = Normalize(exactAddress);
                prefixLength = null;
                return true;
            }

            if (IPNetwork.TryParse(value, out IPNetwork network)
                && !network.BaseAddress.IsIPv4MappedToIPv6)
            {
                address = network.BaseAddress;
                prefixLength = checked((byte)network.PrefixLength);
                return true;
            }

            address = IPAddress.None;
            prefixLength = null;
            return false;
        }

        internal static string GetSuggestedProxyConfiguration(IPAddress address)
        {
            IPAddress normalizedAddress = Normalize(address);
            if (Private172Network.Contains(normalizedAddress))
            {
                return Private172Network.ToString();
            }

            return $"{normalizedAddress}/{GetMaximumPrefixLength(normalizedAddress)}";
        }

        internal static string FormatConfiguredProxy(
            IPAddress address,
            byte? prefixLength = null)
        {
            IPAddress normalizedAddress = Normalize(address);
            if (prefixLength is null)
            {
                return normalizedAddress.ToString();
            }

            if (!TryCreateNetwork(normalizedAddress, prefixLength.Value, out IPNetwork network))
            {
                throw new InvalidOperationException("Trusted proxy prefix length is invalid for its address family.");
            }

            return network.ToString();
        }

        internal static bool MatchesTrustedProxy(
            IPAddress expected,
            byte? prefixLength,
            IPAddress? actual)
        {
            ArgumentNullException.ThrowIfNull(expected);
            if (actual is null)
            {
                return false;
            }

            IPAddress normalizedExpected = Normalize(expected);
            IPAddress normalizedActual = Normalize(actual);
            if (prefixLength is null)
            {
                return normalizedExpected.Equals(normalizedActual);
            }

            if (!TryCreateNetwork(normalizedExpected, prefixLength.Value, out IPNetwork trustedNetwork))
            {
                return false;
            }

            return trustedNetwork.Contains(normalizedActual);
        }

        internal static IPAddress Normalize(IPAddress address)
        {
            ArgumentNullException.ThrowIfNull(address);
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        private static byte GetMaximumPrefixLength(IPAddress address)
        {
            return address.AddressFamily switch
            {
                System.Net.Sockets.AddressFamily.InterNetwork => Ipv4PrefixLength,
                System.Net.Sockets.AddressFamily.InterNetworkV6 => Ipv6PrefixLength,
                _ => throw new InvalidOperationException("Unsupported trusted proxy address family."),
            };
        }

        private static bool TryCreateNetwork(
            IPAddress address,
            byte prefixLength,
            out IPNetwork network)
        {
            return IPNetwork.TryParse($"{address}/{prefixLength}", out network);
        }
    }
}
