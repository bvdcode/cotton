// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;

namespace Cotton.Server.Services.Computation
{
    public class EmbeddingDimensionCache : IDisposable
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
        private readonly SemaphoreSlim _gate = new(1, 1);
        private (Uri Url, string Model, string? Revision, string? Pooling)? _identity;
        private DateTimeOffset _expiresAt;
        private int? _dimensions;

        public async Task<int> GetOrProbeAsync(
            Uri url, ComputationServiceInfo info, bool forceRefresh,
            Func<Task<int>> probe, CancellationToken cancellationToken)
        {
            (Uri Url, string Model, string? Revision, string? Pooling) identity =
                (url, info.ModelId, info.ModelRevision, info.Pooling);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh && _identity == identity
                    && _dimensions is int cached && DateTimeOffset.UtcNow < _expiresAt)
                {
                    return cached;
                }

                _dimensions = null;
                _identity = null;
                int dimensions = await probe();
                _identity = identity;
                _dimensions = dimensions;
                _expiresAt = DateTimeOffset.UtcNow.Add(Lifetime);
                return dimensions;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            _gate.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
