// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Sockets;

namespace Cotton.Sync.Desktop.Composition
{
    internal static class DesktopHttpClientFactory
    {
        private static readonly TimeSpan ConnectFallbackDelay = TimeSpan.FromMilliseconds(250);

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
            IReadOnlyList<IPAddress> orderedAddresses = OrderAddressesForConnect(addresses);
            Exception? lastException = null;
            var attempts = new List<ConnectAttempt>();
            try
            {
                foreach (IPAddress address in orderedAddresses)
                {
                    attempts.Add(StartConnectAttempt(address, context.DnsEndPoint.Port, cancellationToken));
                    Task<ConnectAttempt> completedAttempt = WaitForCompletedConnectAsync(attempts, cancellationToken);
                    Task delay = Task.Delay(ConnectFallbackDelay, cancellationToken);
                    Task completed = await Task.WhenAny(completedAttempt, delay).ConfigureAwait(false);
                    if (completed == completedAttempt)
                    {
                        ConnectAttempt attempt = completedAttempt.Result;
                        if (attempt.ConnectTask.IsCompletedSuccessfully)
                        {
                            return attempt.CreateStream();
                        }

                        lastException = attempt.ConnectTask.Exception?.GetBaseException();
                        attempt.Dispose();
                    }
                }

                while (attempts.Count > 0)
                {
                    ConnectAttempt completedAttempt = await WaitForCompletedConnectAsync(attempts, cancellationToken)
                        .ConfigureAwait(false);
                    if (completedAttempt.ConnectTask.IsCompletedSuccessfully)
                    {
                        return completedAttempt.CreateStream();
                    }

                    lastException = completedAttempt.ConnectTask.Exception?.GetBaseException();
                    completedAttempt.Dispose();
                }
            }
            finally
            {
                foreach (ConnectAttempt attempt in attempts)
                {
                    attempt.Dispose();
                }
            }

            throw lastException ?? new SocketException((int)SocketError.HostNotFound);
        }

        internal static IReadOnlyList<IPAddress> OrderAddressesForConnect(IEnumerable<IPAddress> addresses)
        {
            ArgumentNullException.ThrowIfNull(addresses);
            return addresses
                .Where(static address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .ToArray();
        }

        private static ConnectAttempt StartConnectAttempt(
            IPAddress address,
            int port,
            CancellationToken cancellationToken)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            return new ConnectAttempt(
                address,
                socket,
                socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).AsTask());
        }

        private static async Task<ConnectAttempt> WaitForCompletedConnectAsync(
            List<ConnectAttempt> attempts,
            CancellationToken cancellationToken)
        {
            Task completedTask = await Task.WhenAny(attempts.Select(static attempt => attempt.ConnectTask))
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            ConnectAttempt completedAttempt = attempts.Single(attempt => ReferenceEquals(attempt.ConnectTask, completedTask));
            attempts.Remove(completedAttempt);
            return completedAttempt;
        }

        private sealed class ConnectAttempt : IDisposable
        {
            private bool _completed;

            public ConnectAttempt(IPAddress address, Socket socket, Task connectTask)
            {
                Address = address;
                Socket = socket;
                ConnectTask = connectTask;
            }

            public IPAddress Address { get; }

            public Socket Socket { get; }

            public Task ConnectTask { get; }

            public NetworkStream CreateStream()
            {
                _completed = true;
                return new NetworkStream(Socket, ownsSocket: true);
            }

            public void Dispose()
            {
                if (!_completed)
                {
                    Socket.Dispose();
                }
            }
        }
    }
}
