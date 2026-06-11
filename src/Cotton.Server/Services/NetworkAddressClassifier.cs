// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Sockets;

namespace Cotton.Server.Services
{
    internal static class NetworkAddressClassifier
    {
        public static bool IsLocalNetworkAddress(IPAddress ipAddress)
        {
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (ipAddress.IsIPv4MappedToIPv6)
            {
                ipAddress = ipAddress.MapToIPv4();
            }

            if (IPAddress.IsLoopback(ipAddress))
            {
                return true;
            }

            byte[] bytes = ipAddress.GetAddressBytes();
            return ipAddress.AddressFamily switch
            {
                AddressFamily.InterNetwork => IsPrivateIpv4(bytes) || IsLinkLocalIpv4(bytes),
                AddressFamily.InterNetworkV6 => ipAddress.IsIPv6LinkLocal || IsUniqueLocalIpv6(bytes),
                _ => false
            };
        }

        private static bool IsPrivateIpv4(byte[] bytes)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        private static bool IsLinkLocalIpv4(byte[] bytes)
        {
            return bytes[0] == 169 && bytes[1] == 254;
        }

        private static bool IsUniqueLocalIpv6(byte[] bytes)
        {
            return (bytes[0] & 0xfe) == 0xfc;
        }
    }
}
