// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Sockets;

namespace Cotton.Sync.Desktop.Composition
{
    internal static class DesktopHttpClientFactory
    {
        public static HttpClient Create(TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
            return new HttpClient(CreateHandler(), disposeHandler: true)
            {
                Timeout = timeout,
            };
        }

        private static SocketsHttpHandler CreateHandler()
        {
            return new SocketsHttpHandler
            {
                ConnectCallback = ConnectAsync,
            };
        }

        private static async ValueTask<Stream> ConnectAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(
                    context.DnsEndPoint.Host,
                    cancellationToken)
                .ConfigureAwait(false);
            Exception? lastException = null;
            foreach (IPAddress address in OrderAddressesForConnect(addresses))
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                try
                {
                    await socket.ConnectAsync(
                            new IPEndPoint(address, context.DnsEndPoint.Port),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception exception) when (exception is SocketException or OperationCanceledException)
                {
                    socket.Dispose();
                    lastException = exception;
                    if (exception is OperationCanceledException)
                    {
                        throw;
                    }
                }
            }

            throw lastException ?? new SocketException((int)SocketError.HostNotFound);
        }

        internal static IReadOnlyList<IPAddress> OrderAddressesForConnect(IEnumerable<IPAddress> addresses)
        {
            ArgumentNullException.ThrowIfNull(addresses);
            return addresses
                .OrderBy(static address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .ToArray();
        }
    }
}
