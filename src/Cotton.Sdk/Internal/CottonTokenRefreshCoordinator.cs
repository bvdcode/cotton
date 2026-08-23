// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Runtime.CompilerServices;
using Cotton.Sdk.Auth;

namespace Cotton.Sdk.Internal
{
    internal class CottonTokenRefreshCoordinator
    {
        private static readonly ConditionalWeakTable<ICottonTokenStore, CottonTokenRefreshCoordinator> Coordinators = new();

        private readonly SemaphoreSlim _gate = new(1, 1);
        private string? _previousRefreshToken;
        private string? _currentRefreshToken;

        private CottonTokenRefreshCoordinator()
        {
        }

        public static CottonTokenRefreshCoordinator Get(ICottonTokenStore tokenStore)
        {
            ArgumentNullException.ThrowIfNull(tokenStore);
            return Coordinators.GetValue(tokenStore, static _ => new CottonTokenRefreshCoordinator());
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _gate.WaitAsync(cancellationToken);
        }

        public void Release()
        {
            _gate.Release();
        }

        public bool WasRotated(string refreshToken, string? currentRefreshToken)
        {
            return string.Equals(refreshToken, _previousRefreshToken, StringComparison.Ordinal)
                && string.Equals(currentRefreshToken, _currentRefreshToken, StringComparison.Ordinal);
        }

        public void RecordRotation(string previousRefreshToken, string currentRefreshToken)
        {
            _previousRefreshToken = previousRefreshToken;
            _currentRefreshToken = currentRefreshToken;
        }

        public void ResetRotation()
        {
            _previousRefreshToken = null;
            _currentRefreshToken = null;
        }
    }
}
