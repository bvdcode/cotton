// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Jobs;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class JobStartupDelaysTests
    {
        [Test]
        public async Task WaitOnceAsync_ConcurrentCalls_DelaysOnlyOneCaller()
        {
            int pending = 1;
            using CancellationTokenSource cancellationTokenSource = new();

            Task[] tasks = Enumerable.Range(0, 32)
                .Select(_ => JobStartupDelays.WaitOnceAsync(
                    TimeSpan.FromMinutes(1),
                    ref pending,
                    cancellationTokenSource.Token))
                .ToArray();

            Task delayedTask = tasks.Single(task => !task.IsCompleted);
            Assert.That(tasks.Count(task => task.IsCompletedSuccessfully), Is.EqualTo(tasks.Length - 1));

            await cancellationTokenSource.CancelAsync();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await delayedTask);
            Task subsequentTask = JobStartupDelays.WaitOnceAsync(
                TimeSpan.FromMinutes(1),
                ref pending,
                CancellationToken.None);
            Assert.That(subsequentTask.IsCompletedSuccessfully, Is.True);
        }
    }
}
