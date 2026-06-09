// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Status
{

    /// <summary>
    /// Stores the latest application status and publishes snapshots to subscribers.
    /// </summary>
    public sealed class InMemoryAppStatusPublisher : IAppStatusPublisher
    {
        private readonly object _gate = new();
        private readonly List<IObserver<SyncAppStatus>> _observers = [];
        private SyncAppStatus _current;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryAppStatusPublisher" /> class.
        /// </summary>
        public InMemoryAppStatusPublisher(SyncAppStatus? initialStatus = null)
        {
            _current = initialStatus ?? SyncAppStatus.CreateEmpty();
        }

        /// <inheritdoc />
        public SyncAppStatus Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<SyncAppStatus> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            SyncAppStatus current;
            lock (_gate)
            {
                _observers.Add(observer);
                current = _current;
            }

            observer.OnNext(current);
            return new Subscription(this, observer);
        }

        /// <inheritdoc />
        public void Publish(SyncAppStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);
            IObserver<SyncAppStatus>[] observers;
            lock (_gate)
            {
                _current = status;
                observers = _observers.ToArray();
            }

            foreach (IObserver<SyncAppStatus> observer in observers)
            {
                observer.OnNext(status);
            }
        }

        private void Unsubscribe(IObserver<SyncAppStatus> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly IObserver<SyncAppStatus> _observer;
            private readonly InMemoryAppStatusPublisher _publisher;
            private bool _disposed;

            public Subscription(InMemoryAppStatusPublisher publisher, IObserver<SyncAppStatus> observer)
            {
                _publisher = publisher;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _publisher.Unsubscribe(_observer);
                _disposed = true;
            }
        }
    }
}
