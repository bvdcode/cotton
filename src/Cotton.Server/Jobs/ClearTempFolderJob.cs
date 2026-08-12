// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 36)]
    public class ClearTempFolderJob(PerfTracker _perf, IStorageBackendProvider _backendProvider) : IJob
    {
        private static readonly TimeSpan _ttl = TimeSpan.FromHours(1);

        public async Task Execute(IJobExecutionContext context)
        {
            await JobStartupDelays.WaitForClearTempFolderAsync(context.CancellationToken);

            if (_perf.IsNightTime())
            {
                return;
            }

            IStorageBackend backend = _backendProvider.GetBackend();
            backend.CleanupTempFiles(_ttl);
        }
    }
}
