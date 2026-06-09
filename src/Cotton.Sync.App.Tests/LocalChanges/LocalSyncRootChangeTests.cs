// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.LocalChanges;

namespace Cotton.Sync.App.Tests.LocalChanges
{
    public sealed class LocalSyncRootChangeTests
    {
        [Test]
        public void Constructor_RejectsUnknownKind()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LocalSyncRootChange(
                Guid.NewGuid(),
                "/tmp/report.txt",
                LocalSyncRootChangeKind.Unknown));
        }
    }
}
