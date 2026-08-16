// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Pipelines
{
    public class StorageWriteAdmissionGate
    {
        private readonly SemaphoreSlim _semaphore;

        public StorageWriteAdmissionGate(int maxParallelWrites)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelWrites);
            _semaphore = new SemaphoreSlim(maxParallelWrites, maxParallelWrites);
        }

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            return _semaphore.WaitAsync(cancellationToken);
        }

        public void Release()
        {
            _semaphore.Release();
        }
    }
}
