// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Runs the scheduled clear temp folder maintenance task.
    /// </summary>
    [JobTrigger(hours: 36)]
    public class ClearTempFolderJob(PerfTracker _perf, IStorageBackendProvider _backendProvider) : IJob
    {
        private static readonly TimeSpan _ttl = TimeSpan.FromHours(1);

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(420_000); // Wait for 7 minutes for the server to start up and stabilize

            if (_perf.IsNightTime())
            {
                return;
            }

            IStorageBackend backend = _backendProvider.GetBackend();
            backend.CleanupTempFiles(_ttl);
        }
    }
}
