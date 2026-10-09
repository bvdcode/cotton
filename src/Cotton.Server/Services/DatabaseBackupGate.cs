// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class DatabaseBackupGate : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public Task WaitAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);

        public void Release() => _gate.Release();

        public void Dispose() => _gate.Dispose();
    }
}
