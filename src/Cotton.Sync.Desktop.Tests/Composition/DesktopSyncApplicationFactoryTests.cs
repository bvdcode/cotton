// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Reflection;
using Cotton.Sdk;
using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.RemoteChanges;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.Desktop.Composition;

namespace Cotton.Sync.Desktop.Tests.Composition
{
    public sealed class DesktopSyncApplicationFactoryTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-desktop-composition-" + Guid.NewGuid().ToString("N"));
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
        public async Task Create_TransfersCottonClientOwnershipToHost()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var factory = new DesktopSyncApplicationFactory(paths);

            await using DesktopSyncApplicationHost host = factory.Create(new Uri("https://cotton.example.test/"));

            object asyncResource = GetPrivateFieldValue(host, "_asyncResource");

            Assert.That(asyncResource, Is.TypeOf<CottonCloudClient>());
        }

        [Test]
        public async Task Create_WiresContinuousSyncCoordinators()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var factory = new DesktopSyncApplicationFactory(paths);

            await using DesktopSyncApplicationHost host = factory.Create(new Uri("https://cotton.example.test/"));

            Assert.That(host.App, Is.TypeOf<SyncApplicationService>());
            object localChanges = GetPrivateFieldValue(host.App, "_localChanges");
            object remoteChanges = GetPrivateFieldValue(host.App, "_remoteChanges");
            object periodicSync = GetPrivateFieldValue(host.App, "_periodicSync");

            Assert.Multiple(() =>
            {
                Assert.That(localChanges, Is.TypeOf<LocalChangeSyncCoordinator>());
                Assert.That(remoteChanges, Is.TypeOf<RealtimeRemoteChangeSyncCoordinator>());
                Assert.That(periodicSync, Is.TypeOf<PeriodicSyncCoordinator>());
            });
        }

        private static object GetPrivateFieldValue(object instance, string fieldName)
        {
            FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field!.GetValue(instance) ?? throw new InvalidOperationException(fieldName);
        }
    }
}
