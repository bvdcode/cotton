// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.SyncPairs
{
    public class SyncPairPrerequisiteValidatorTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-sync-app-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public async Task ValidateAsync_ReturnsLocalAndRemotePrerequisiteErrors()
        {
            SyncPairSettings syncPair = CreatePair(LocalPath("Cotton"));
            var validator = new SyncPairPrerequisiteValidator(
                new FakeLocalSyncRootProbe(canUse: false),
                new FakeRemoteSyncRootProbe(exists: false));

            IReadOnlyList<SyncPairValidationError> errors = await validator.ValidateAsync(syncPair);

            Assert.That(errors.Select(error => error.Issue), Is.EqualTo(new[]
            {
                SyncPairValidationIssue.LocalRootUnavailable,
                SyncPairValidationIssue.RemoteRootUnavailable,
            }));
        }

        [Test]
        public async Task FileSystemLocalSyncRootProbe_CreatesMissingDirectory()
        {
            string localRoot = LocalPath("NewRoot");
            var probe = new FileSystemLocalSyncRootProbe();

            bool canUse = await probe.CanUseAsync(localRoot);

            Assert.Multiple(() =>
            {
                Assert.That(canUse, Is.True);
                Assert.That(Directory.Exists(localRoot), Is.True);
            });
        }

        [Test]
        public async Task FileSystemLocalSyncRootProbe_ReturnsFalseForExistingFile()
        {
            string filePath = LocalPath("not-a-directory");
            File.WriteAllText(filePath, "content");
            var probe = new FileSystemLocalSyncRootProbe();

            bool canUse = await probe.CanUseAsync(filePath);

            Assert.That(canUse, Is.False);
        }

        private string LocalPath(string name)
        {
            return Path.Combine(_tempDirectory, name);
        }

        private static SyncPairSettings CreatePair(string localRootPath)
        {
            return new SyncPairSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = "Documents",
                LocalRootPath = localRootPath,
                RemoteRootNodeId = Guid.NewGuid(),
                RemoteDisplayPath = "/Documents",
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
            };
        }

        private class FakeLocalSyncRootProbe : ILocalSyncRootProbe
        {
            private readonly bool _canUse;

            public FakeLocalSyncRootProbe(bool canUse)
            {
                _canUse = canUse;
            }

            public Task<bool> CanUseAsync(string localRootPath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_canUse);
            }
        }

        private class FakeRemoteSyncRootProbe : IRemoteSyncRootProbe
        {
            private readonly bool _exists;

            public FakeRemoteSyncRootProbe(bool exists)
            {
                _exists = exists;
            }

            public Task<bool> ExistsAsync(Guid remoteRootNodeId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_exists);
            }
        }
    }
}
