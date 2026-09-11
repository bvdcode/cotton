// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.WebDav;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class WebDavLockManagerTests
    {
        [Test]
        public void RequiresAncestorLockTokenUntilUnlocked()
        {
            WebDavLockManager locks = new();
            Guid userId = Guid.NewGuid();
            WebDavLockInfo lockInfo = locks.Create(userId, "folder", TimeSpan.FromMinutes(1));

            Assert.Multiple(() =>
            {
                Assert.That(locks.IsSatisfied(userId, "folder/file.txt", null), Is.False);
                Assert.That(locks.IsSatisfied(userId, "folder/file.txt", lockInfo.Token), Is.True);
            });

            locks.Unlock(userId, "folder", lockInfo.Token);
            Assert.That(locks.IsSatisfied(userId, "folder/file.txt", null), Is.True);
        }
    }
}
