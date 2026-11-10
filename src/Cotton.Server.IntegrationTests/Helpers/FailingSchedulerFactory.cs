// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Quartz;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class FailingSchedulerFactory : ISchedulerFactory
    {
        public int Attempts { get; private set; }

        public Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new SchedulerException("Scheduler unavailable");
        }

        public Task<IScheduler?> GetScheduler(string schedName, CancellationToken cancellationToken = default)
            => throw new SchedulerException("Scheduler unavailable");

        public Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IScheduler>>([]);
    }
}
