// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class UserStorageQuotaMutationGateTests
    {
        [Test]
        public async Task EnterAsync_SerializesSameUserWithoutBlockingOtherUsers()
        {
            UserStorageQuotaMutationGate gate = new();
            Guid firstUserId = Guid.NewGuid();
            Task<IAsyncDisposable> sameUser;

            await using (IAsyncDisposable first = await gate.EnterAsync(firstUserId))
            {
                sameUser = gate.EnterAsync(firstUserId).AsTask();
                await using IAsyncDisposable otherUser = await gate.EnterAsync(Guid.NewGuid());

                Assert.Multiple(() =>
                {
                    Assert.That(sameUser.IsCompleted, Is.False);
                    Assert.That(gate.Count, Is.EqualTo(2));
                });
            }

            await using (IAsyncDisposable admitted = await sameUser.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                Assert.That(gate.Count, Is.EqualTo(1));
            }

            Assert.That(gate.Count, Is.Zero);
        }
    }
}
