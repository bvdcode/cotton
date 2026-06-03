// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Activities;

/// <summary>
/// Publishes live activity entries to in-process subscribers.
/// </summary>
public sealed class InMemoryAppActivityPublisher : IAppActivityPublisher
{
    private readonly object _gate = new();
    private readonly List<IObserver<SyncActivity>> _observers = [];

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<SyncActivity> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    /// <inheritdoc />
    public void Publish(SyncActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        IObserver<SyncActivity>[] observers;
        lock (_gate)
        {
            observers = [.. _observers];
        }

        foreach (IObserver<SyncActivity> observer in observers)
        {
            observer.OnNext(activity);
        }
    }

    private void Unsubscribe(IObserver<SyncActivity> observer)
    {
        lock (_gate)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryAppActivityPublisher _publisher;
        private IObserver<SyncActivity>? _observer;

        public Subscription(InMemoryAppActivityPublisher publisher, IObserver<SyncActivity> observer)
        {
            _publisher = publisher;
            _observer = observer;
        }

        public void Dispose()
        {
            IObserver<SyncActivity>? observer = Interlocked.Exchange(ref _observer, null);
            if (observer is not null)
            {
                _publisher.Unsubscribe(observer);
            }
        }
    }
}
