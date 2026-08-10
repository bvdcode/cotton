// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Stores pending app-code authorization requests for this server process.
    /// </summary>
    public class AppCodeRequestStore : IDisposable
    {
        internal const int MaxActiveRequests = 1024;
        private static readonly TimeSpan ExpirationScanFrequency = TimeSpan.FromSeconds(30);
        private readonly Lock _gate = new();
        private readonly MemoryCache _requests = new(new MemoryCacheOptions
        {
            SizeLimit = MaxActiveRequests,
            ExpirationScanFrequency = ExpirationScanFrequency,
        });

        internal bool TryAdd(AppCodeRequestState state)
        {
            lock (_gate)
            {
                if (_requests.Count >= MaxActiveRequests)
                {
                    return false;
                }

                _requests.Set(state.ApprovalId, state, CreateCacheEntryOptions(state.ExpiresAt));
                return true;
            }
        }

        internal bool TryGet(Guid approvalId, out AppCodeRequestState? state)
        {
            return _requests.TryGetValue(approvalId, out state);
        }

        internal void Remove(AppCodeRequestState state)
        {
            state.Tokens = null;
            state.Completion.TrySetResult();
            _requests.Remove(state.ApprovalId);
        }

        /// <summary>
        /// Releases the request cache.
        /// </summary>
        public void Dispose()
        {
            _requests.Dispose();
            GC.SuppressFinalize(this);
        }

        private static MemoryCacheEntryOptions CreateCacheEntryOptions(DateTime expiresAt)
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiresAt)
                .SetSize(1)
                .RegisterPostEvictionCallback(static (_, value, _, _) =>
                {
                    if (value is not AppCodeRequestState state)
                    {
                        return;
                    }

                    state.Tokens = null;
                    state.Completion.TrySetResult();
                });
        }
    }
}
