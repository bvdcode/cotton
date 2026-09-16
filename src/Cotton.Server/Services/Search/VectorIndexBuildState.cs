// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public class VectorIndexBuildState
    {
        private readonly Lock _lock = new();
        private bool _running;
        private string? _errorCode;

        public (bool Running, string? ErrorCode) GetSnapshot()
        {
            lock (_lock)
            {
                return (_running, _errorCode);
            }
        }

        public bool TryStart()
        {
            lock (_lock)
            {
                if (_running)
                {
                    return false;
                }
                _running = true;
                _errorCode = null;
                return true;
            }
        }

        public void Complete(string? errorCode = null)
        {
            lock (_lock)
            {
                _running = false;
                _errorCode = errorCode;
            }
        }
    }
}
