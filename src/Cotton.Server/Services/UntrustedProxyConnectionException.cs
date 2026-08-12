// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;

namespace Cotton.Server.Services
{
    public class UntrustedProxyConnectionException(
        IPAddress trustedProxyIpAddress,
        byte? trustedProxyPrefixLength,
        IPAddress? connectingIpAddress) : Exception(
            "The request did not arrive through the configured trusted reverse proxy.")
    {
        public IPAddress TrustedProxyIpAddress { get; } = trustedProxyIpAddress;

        public byte? TrustedProxyPrefixLength { get; } = trustedProxyPrefixLength;

        public IPAddress? ConnectingIpAddress { get; } = connectingIpAddress;
    }
}
