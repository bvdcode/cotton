// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Activities;

/// <summary>
/// Publishes live activity entries to in-process subscribers.
/// </summary>
public sealed class InMemoryAppActivityPublisher : IAppActivityPublisher
{
    private readonly object _gate = new();
    private readonly List<IObserver<AppSyncActivity>> _observers = [];

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<AppSyncActivity> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    /// <inheritdoc />
    public void Publish(AppSyncActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        IObserver<AppSyncActivity>[] observers;
        lock (_gate)
        {
            observers = [.. _observers];
        }

        foreach (IObserver<AppSyncActivity> observer in observers)
        {
            observer.OnNext(activity);
        }
    }

    private void Unsubscribe(IObserver<AppSyncActivity> observer)
    {
        lock (_gate)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryAppActivityPublisher _publisher;
        private IObserver<AppSyncActivity>? _observer;

        public Subscription(InMemoryAppActivityPublisher publisher, IObserver<AppSyncActivity> observer)
        {
            _publisher = publisher;
            _observer = observer;
        }

        public void Dispose()
        {
            IObserver<AppSyncActivity>? observer = Interlocked.Exchange(ref _observer, null);
            if (observer is not null)
            {
                _publisher.Unsubscribe(observer);
            }
        }
    }
}
