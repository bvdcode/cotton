// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Realtime
{
    /// <summary>
    /// Provides realtime Cotton event hub operations.
    /// </summary>
    public interface ICottonRealtimeClient : IAsyncDisposable
    {
        /// <summary>
        /// Occurs when a remote file-tree mutation event is received.
        /// </summary>
        event EventHandler<CottonRealtimeEvent>? RemoteFileTreeChanged;

        /// <summary>
        /// Occurs when the current auth session is revoked by the server.
        /// </summary>
        event EventHandler<CottonRealtimeEvent>? SessionRevoked;

        /// <summary>
        /// Occurs when the server reports a newly created notification.
        /// </summary>
        event EventHandler<CottonRealtimeEvent>? NotificationReceived;

        /// <summary>
        /// Occurs after an interrupted connection is restored.
        /// </summary>
        event EventHandler? Reconnected;

        /// <summary>
        /// Occurs when the connection stops, including exhausted automatic reconnect attempts.
        /// </summary>
        event EventHandler<Exception?>? Closed;

        /// <summary>
        /// Starts the realtime connection.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the realtime connection.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
