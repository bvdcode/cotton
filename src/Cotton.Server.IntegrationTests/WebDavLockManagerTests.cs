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

        [TestCase("Folder/Résumé.txt", "folder/resume.txt")]
        [TestCase("café.txt", "cafe\u0301.txt")]
        [TestCase("folder/file.txt", "folder//file.txt")]
        [TestCase("folder/file.txt", "folder\\file.txt")]
        [TestCase("folder/file.txt", " folder / file.txt. ")]
        public void EquivalentPaths_RequireSameLockToken(string path, string equivalentPath)
        {
            WebDavLockManager locks = new();
            Guid userId = Guid.NewGuid();
            WebDavLockInfo lockInfo = locks.Create(userId, path, TimeSpan.FromMinutes(1));

            Assert.Multiple(() =>
            {
                Assert.That(locks.IsSatisfied(userId, equivalentPath, null), Is.False);
                Assert.That(locks.IsSatisfied(userId, equivalentPath, lockInfo.Token), Is.True);
            });

            locks.Unlock(userId, equivalentPath, lockInfo.Token);
            Assert.That(locks.IsSatisfied(userId, path, null), Is.True);
        }

        [Test]
        public void LiteralPercentName_HasSeparateLock()
        {
            WebDavLockManager locks = new();
            Guid userId = Guid.NewGuid();
            locks.Create(userId, "invoiceA.txt", TimeSpan.FromMinutes(1));

            Assert.That(locks.IsSatisfied(userId, "invoice%41.txt", null), Is.True);
        }
    }
}
