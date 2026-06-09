// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress
{
    /// <summary>
    /// Publishes aggregate sync-pass progress entries to in-process subscribers.
    /// </summary>
    public class InMemoryAppRunProgressPublisher : IAppRunProgressPublisher
    {
        private readonly object _gate = new();
        private readonly List<IObserver<AppRunProgress>> _observers = [];

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<AppRunProgress> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_gate)
            {
                _observers.Add(observer);
            }

            return new Cotton.Sync.App.ObservableSubscription<AppRunProgress>(Unsubscribe, observer);
        }

        /// <inheritdoc />
        public void Publish(AppRunProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            IObserver<AppRunProgress>[] observers;
            lock (_gate)
            {
                observers = [.. _observers];
            }

            foreach (IObserver<AppRunProgress> observer in observers)
            {
                observer.OnNext(progress);
            }
        }

        private void Unsubscribe(IObserver<AppRunProgress> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

    }
}
