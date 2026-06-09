// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Tests.TestSupport
{
    internal sealed class RecordingObserver<T> : IObserver<T>
    {
        private readonly List<T> _values = [];

        public IReadOnlyList<T> Values => _values;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public void OnNext(T value)
        {
            _values.Add(value);
        }
    }
}
