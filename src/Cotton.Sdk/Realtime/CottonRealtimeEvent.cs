// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Realtime
{
    /// <summary>
    /// Represents one realtime event received from the Cotton event hub.
    /// </summary>
    public class CottonRealtimeEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CottonRealtimeEvent" /> class.
        /// </summary>
        public CottonRealtimeEvent(CottonRealtimeEventKind kind, string methodName, DateTime receivedAtUtc)
        {
            if (kind == CottonRealtimeEventKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Realtime event kind must be known.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            Kind = kind;
            MethodName = methodName;
            ReceivedAtUtc = receivedAtUtc.Kind == DateTimeKind.Utc
                ? receivedAtUtc
                : receivedAtUtc.ToUniversalTime();
        }

        /// <summary>
        /// Gets the event kind.
        /// </summary>
        public CottonRealtimeEventKind Kind { get; }

        /// <summary>
        /// Gets the hub method name that delivered the event.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets the UTC timestamp when the SDK received the event.
        /// </summary>
        public DateTime ReceivedAtUtc { get; }
    }
}
