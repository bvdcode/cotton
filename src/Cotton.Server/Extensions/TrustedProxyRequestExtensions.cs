// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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
        /// <summary>
        /// Gets the client address supplied by a configured trusted proxy, or preserves legacy header trust while no
        /// trusted proxy has been configured.
        /// </summary>
        public static IPAddress GetTrustedClientIPAddress(this HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            SettingsProvider settingsProvider = request.HttpContext.RequestServices
                .GetRequiredService<SettingsProvider>();
            IPAddress? trustedProxyIpAddress = settingsProvider.GetServerSettings().TrustedProxyIpAddress;
            return request.GetTrustedClientIPAddress(trustedProxyIpAddress);
        }

        internal static IPAddress GetTrustedClientIPAddress(
            this HttpRequest request,
            IPAddress? trustedProxyIpAddress)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (trustedProxyIpAddress is not null)
            {
                IPAddress? connectingIpAddress = request.GetConnectingIPAddress();
                if (!AddressesEqual(trustedProxyIpAddress, connectingIpAddress))
                {
                    throw new UntrustedProxyConnectionException(
                        Normalize(trustedProxyIpAddress),
                        connectingIpAddress);
                }
            }

            // EasyExtensions currently resolves CF-Connecting-IP, then X-Real-IP, then X-Forwarded-For, and finally
            // Connection.RemoteIpAddress. The configured peer check above is the trust boundary around those headers.
            return EasyHttpRequestExtensions.GetRemoteIPAddress(request);
        }

        internal static IPAddress? GetConnectingIPAddress(this HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            IPAddress? address = request.HttpContext.Connection.RemoteIpAddress;
            return address is null ? null : Normalize(address);
        }

        internal static bool AddressesEqual(IPAddress expected, IPAddress? actual)
        {
            ArgumentNullException.ThrowIfNull(expected);
            return actual is not null && Normalize(expected).Equals(Normalize(actual));
        }

        internal static IPAddress Normalize(IPAddress address)
        {
            ArgumentNullException.ThrowIfNull(address);
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }
    }
}
