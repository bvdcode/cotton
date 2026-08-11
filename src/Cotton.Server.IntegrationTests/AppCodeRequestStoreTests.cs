// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    [TestFixture]
    public class AppCodeRequestStoreTests
    {
        [Test]
        public void StoreInstances_DoNotShareRequests()
        {
            using AppCodeRequestStore firstStore = new();
            using AppCodeRequestStore secondStore = new();
            AppCodeRequestState state = CreateState();

            bool added = firstStore.TryAdd(state);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.True);
                Assert.That(firstStore.TryGet(state.ApprovalId, out _), Is.True);
                Assert.That(secondStore.TryGet(state.ApprovalId, out _), Is.False);
            });
        }

        [Test]
        public void TryAdd_RejectsRequestsBeyondCapacity()
        {
            using AppCodeRequestStore store = new();
            for (int i = 0; i < AppCodeRequestStore.MaxActiveRequests; i++)
            {
                Assert.That(store.TryAdd(CreateState()), Is.True);
            }

            Assert.That(store.TryAdd(CreateState()), Is.False);
        }

        [Test]
        public void Remove_DeletesRequestAndSignalsWaiters()
        {
            using AppCodeRequestStore store = new();
            AppCodeRequestState state = CreateState();
            Assert.That(store.TryAdd(state), Is.True);

            store.Remove(state);

            Assert.Multiple(() =>
            {
                Assert.That(store.TryGet(state.ApprovalId, out _), Is.False);
                Assert.That(state.Completion.Task.IsCompleted, Is.True);
            });
        }

        private static AppCodeRequestState CreateState()
        {
            return new AppCodeRequestState(
                Guid.NewGuid(),
                [1],
                "Cotton Sync",
                "1.0.0",
                "Test device",
                "127.0.0.1",
                "test-agent",
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(10));
        }
    }
}
