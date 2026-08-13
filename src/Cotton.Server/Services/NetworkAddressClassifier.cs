// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Sockets;

namespace Cotton.Server.Services
{
    internal static class NetworkAddressClassifier
    {
        private static readonly IPNetwork[] NonPublicIpv4Networks =
        [
            IPNetwork.Parse("0.0.0.0/8"),
            IPNetwork.Parse("10.0.0.0/8"),
            IPNetwork.Parse("100.64.0.0/10"),
            IPNetwork.Parse("127.0.0.0/8"),
            IPNetwork.Parse("169.254.0.0/16"),
            IPNetwork.Parse("172.16.0.0/12"),
            IPNetwork.Parse("192.0.0.0/24"),
            IPNetwork.Parse("192.0.2.0/24"),
            IPNetwork.Parse("192.88.99.0/24"),
            IPNetwork.Parse("192.168.0.0/16"),
            IPNetwork.Parse("198.18.0.0/15"),
            IPNetwork.Parse("198.51.100.0/24"),
            IPNetwork.Parse("203.0.113.0/24"),
            IPNetwork.Parse("224.0.0.0/4"),
            IPNetwork.Parse("240.0.0.0/4"),
        ];

        private static readonly IPNetwork PublicIpv6Network = IPNetwork.Parse("2000::/3");

        private static readonly IPNetwork[] NonPublicIpv6Networks =
        [
            IPNetwork.Parse("2001:db8::/32"),
            IPNetwork.Parse("2002::/16"),
        ];

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

        public static bool IsPublicInternetAddress(IPAddress ipAddress)
        {
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (ipAddress.IsIPv4MappedToIPv6)
            {
                ipAddress = ipAddress.MapToIPv4();
            }

            return ipAddress.AddressFamily switch
            {
                AddressFamily.InterNetwork => !Contains(NonPublicIpv4Networks, ipAddress),
                AddressFamily.InterNetworkV6 => PublicIpv6Network.Contains(ipAddress)
                    && !Contains(NonPublicIpv6Networks, ipAddress),
                _ => false,
            };
        }

        private static bool Contains(IEnumerable<IPNetwork> networks, IPAddress ipAddress)
        {
            foreach (IPNetwork network in networks)
            {
                if (network.Contains(ipAddress))
                {
                    return true;
                }
            }

            return false;
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
