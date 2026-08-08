// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Common
{
    internal class QuotaMutationBarrier
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;
        private int _enabled;

        public void Enable()
        {
            Interlocked.Exchange(ref _enabled, 1);
        }

        public async Task SignalAndWaitIfEnabledAsync(CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _enabled) == 0)
            {
                return;
            }

            int arrived = Interlocked.Increment(ref _arrived);
            if (arrived == 2)
            {
                _release.TrySetResult();
            }
            else if (arrived > 2)
            {
                throw new InvalidOperationException("The two-participant barrier was entered more than twice.");
            }

            await _release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}
