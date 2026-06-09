// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Publishes server-driven authentication session revocation events.
    /// </summary>
    public interface ISessionRevocationPublisher : IObservable<SessionRevocationEvent>
    {
        /// <summary>
        /// Publishes one session revocation event.
        /// </summary>
        void Publish(SessionRevocationEvent sessionRevocation);
    }
}
