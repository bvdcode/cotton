// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Indicates that a client-address lookup did not arrive through the configured immediate reverse proxy.
    /// </summary>
    public class UntrustedProxyConnectionException(
        IPAddress trustedProxyIpAddress,
        byte? trustedProxyPrefixLength,
        IPAddress? connectingIpAddress) : Exception(
            "The request did not arrive through the configured trusted reverse proxy.")
    {
        /// <summary>
        /// Gets the configured immediate reverse-proxy address.
        /// </summary>
        public IPAddress TrustedProxyIpAddress { get; } = trustedProxyIpAddress;

        /// <summary>
        /// Gets the configured trusted network prefix length, or null when an exact address is required.
        /// </summary>
        public byte? TrustedProxyPrefixLength { get; } = trustedProxyPrefixLength;

        /// <summary>
        /// Gets the address of the peer that opened the TCP connection, when available.
        /// </summary>
        public IPAddress? ConnectingIpAddress { get; } = connectingIpAddress;
    }
}
