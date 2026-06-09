// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Publishes session revocation events to in-process subscribers.
    /// </summary>
    public class InMemorySessionRevocationPublisher : ISessionRevocationPublisher
    {
        private readonly object _gate = new();
        private readonly List<IObserver<SessionRevocationEvent>> _observers = [];

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<SessionRevocationEvent> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_gate)
            {
                _observers.Add(observer);
            }

            return new Cotton.Sync.App.ObservableSubscription<SessionRevocationEvent>(Unsubscribe, observer);
        }

        /// <inheritdoc />
        public void Publish(SessionRevocationEvent sessionRevocation)
        {
            ArgumentNullException.ThrowIfNull(sessionRevocation);
            IObserver<SessionRevocationEvent>[] observers;
            lock (_gate)
            {
                observers = [.. _observers];
            }

            foreach (IObserver<SessionRevocationEvent> observer in observers)
            {
                observer.OnNext(sessionRevocation);
            }
        }

        private void Unsubscribe(IObserver<SessionRevocationEvent> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

    }
}
