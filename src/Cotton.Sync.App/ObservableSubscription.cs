// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App
{
    internal class ObservableSubscription<T> : IDisposable
    {
        private readonly Action<IObserver<T>> _unsubscribe;
        private IObserver<T>? _observer;

        public ObservableSubscription(Action<IObserver<T>> unsubscribe, IObserver<T> observer)
        {
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public void Dispose()
        {
            IObserver<T>? observer = Interlocked.Exchange(ref _observer, null);
            if (observer is not null)
            {
                _unsubscribe(observer);
            }
        }
    }
}
