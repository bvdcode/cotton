// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Desktop.Shell
{
    internal class DesktopShellObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public DesktopShellObserver(Action<T> onNext)
        {
            _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Trace.TraceError(error.ToString());
        }

        public void OnNext(T value)
        {
            _onNext(value);
        }
    }
}
