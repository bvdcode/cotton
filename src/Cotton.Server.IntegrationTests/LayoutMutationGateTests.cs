// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class LayoutMutationGateTests
    {
        [Test]
        public async Task EnterAsync_ReentrantLeaseKeepsEntryUntilOutermostRelease()
        {
            LayoutMutationGate gate = new();
            Guid layoutId = Guid.NewGuid();
            IAsyncDisposable outerLease = await gate.EnterAsync(layoutId, CancellationToken.None);
            IAsyncDisposable innerLease = await gate.EnterAsync(layoutId, CancellationToken.None);

            Assert.That(gate.Count, Is.EqualTo(1));

            await innerLease.DisposeAsync();
            Assert.That(gate.Count, Is.EqualTo(1));

            await outerLease.DisposeAsync();
            Assert.That(gate.Count, Is.Zero);
        }

        [Test]
        public async Task EnterAsync_IndependentCallersSerializeAndReleaseEntry()
        {
            LayoutMutationGate gate = new();
            Guid layoutId = Guid.NewGuid();
            IAsyncDisposable firstLease = await gate.EnterAsync(layoutId, CancellationToken.None);
            TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task secondCaller = RunWithoutExecutionContextAsync(async () =>
            {
                waiting.SetResult();
                await using IAsyncDisposable secondLease = await gate.EnterAsync(layoutId, CancellationToken.None);
                acquired.SetResult();
                await release.Task;
            });

            await waiting.Task;
            Assert.That(acquired.Task.IsCompleted, Is.False);

            await firstLease.DisposeAsync();
            await acquired.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.That(gate.Count, Is.EqualTo(1));

            release.SetResult();
            await secondCaller.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(gate.Count, Is.Zero);
        }

        [Test]
        public void EnterAsync_CanceledWaitDoesNotRetainEntry()
        {
            LayoutMutationGate gate = new();
            Guid layoutId = Guid.NewGuid();
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.That(
                async () => await gate.EnterAsync(layoutId, cancellation.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(gate.Count, Is.Zero);
        }

        private static Task RunWithoutExecutionContextAsync(Func<Task> action)
        {
            using (ExecutionContext.SuppressFlow())
            {
                return Task.Run(action);
            }
        }
    }
}
