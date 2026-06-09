// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress
{
    /// <summary>
    /// Publishes live transfer progress entries to in-process subscribers.
    /// </summary>
    public class InMemoryAppTransferProgressPublisher : IAppTransferProgressPublisher
    {
        private readonly object _gate = new();
        private readonly List<IObserver<AppTransferProgress>> _observers = [];

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<AppTransferProgress> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_gate)
            {
                _observers.Add(observer);
            }

            return new Cotton.Sync.App.ObservableSubscription<AppTransferProgress>(Unsubscribe, observer);
        }

        /// <inheritdoc />
        public void Publish(AppTransferProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            IObserver<AppTransferProgress>[] observers;
            lock (_gate)
            {
                observers = [.. _observers];
            }

            foreach (IObserver<AppTransferProgress> observer in observers)
            {
                observer.OnNext(progress);
            }
        }

        private void Unsubscribe(IObserver<AppTransferProgress> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

    }
}
