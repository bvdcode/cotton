// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Sockets;

namespace Cotton.Server.Services
{
    internal static class OidcAvatarConnectionPolicy
    {
        public static HttpRequestOptionsKey<DnsEndPoint> TrustedPrivateEndpointOption { get; } =
            new("Cotton.OidcAvatar.TrustedPrivateEndpoint");

        public static async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            IPAddress[] resolvedAddresses = await Dns.GetHostAddressesAsync(
                context.DnsEndPoint.Host,
                cancellationToken);
            context.InitialRequestMessage.Options.TryGetValue(
                TrustedPrivateEndpointOption,
                out DnsEndPoint? trustedPrivateEndpoint);
            IPAddress[] allowedAddresses = SelectAllowedAddresses(
                resolvedAddresses,
                context.DnsEndPoint,
                trustedPrivateEndpoint);
            if (allowedAddresses.Length == 0)
            {
                throw new HttpRequestException(
                    $"OIDC avatar host '{context.DnsEndPoint.Host}' resolved only to non-public addresses.");
            }

            Socket socket = new(SocketType.Stream, ProtocolType.Tcp);
            bool connected = false;
            try
            {
                await socket.ConnectAsync(
                    allowedAddresses,
                    context.DnsEndPoint.Port,
                    cancellationToken);
                connected = true;
                return new NetworkStream(socket, ownsSocket: true);
            }
            finally
            {
                if (!connected)
                {
                    socket.Dispose();
                }
            }
        }

        internal static IPAddress[] SelectAllowedAddresses(
            IEnumerable<IPAddress> addresses,
            DnsEndPoint destination,
            DnsEndPoint? trustedPrivateEndpoint)
        {
            bool trustsDestination = trustedPrivateEndpoint is not null
                && trustedPrivateEndpoint.Port == destination.Port
                && string.Equals(
                    trustedPrivateEndpoint.Host,
                    destination.Host,
                    StringComparison.OrdinalIgnoreCase);
            List<IPAddress> allowedAddresses = [];
            foreach (IPAddress address in addresses)
            {
                if (trustsDestination || NetworkAddressClassifier.IsPublicInternetAddress(address))
                {
                    allowedAddresses.Add(address);
                }
            }

            return [.. allowedAddresses];
        }
    }
}
