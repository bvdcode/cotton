// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class ServerSettingsCacheTests
    {
        [Test]
        public void GetOrAdd_CachesImmutableSnapshot()
        {
            ServerSettingsCache cache = new();
            CottonServerSettings entity = CreateSettings(encryptionThreads: 2, ServerUsage.Photos);

            ServerSettingsSnapshot first = cache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(entity));
            entity.EncryptionThreads = 8;
            entity.ServerUsage[0] = ServerUsage.Media;
            ServerSettingsSnapshot second = cache.GetOrAdd(
                () => throw new AssertionException("The cached snapshot should be reused."));

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.SameAs(first));
                Assert.That(second.EncryptionThreads, Is.EqualTo(2));
                Assert.That(second.ServerUsage, Is.EqualTo(new[] { ServerUsage.Photos }));
                Assert.That(cache.GetEncryptionThreads(), Is.EqualTo(2));
            });
        }

        [Test]
        public void InvalidateSettings_ReloadsSnapshot()
        {
            ServerSettingsCache cache = new();
            ServerSettingsSnapshot first = cache.GetOrAdd(
                () => ServerSettingsSnapshot.FromEntity(CreateSettings(2, ServerUsage.Photos)));

            cache.InvalidateSettings(serverIsInitialized: true);

            ServerSettingsSnapshot second = cache.GetOrAdd(
                () => ServerSettingsSnapshot.FromEntity(CreateSettings(4, ServerUsage.Documents)));

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(second.EncryptionThreads, Is.EqualTo(4));
                Assert.That(second.ServerUsage, Is.EqualTo(new[] { ServerUsage.Documents }));
                Assert.That(cache.TryGetServerInitialized(out bool initialized), Is.True);
                Assert.That(initialized, Is.True);
            });
        }

        [Test]
        public void Instances_DoNotShareState()
        {
            ServerSettingsCache firstCache = new();
            ServerSettingsCache secondCache = new();

            ServerSettingsSnapshot first = firstCache.GetOrAdd(
                () => ServerSettingsSnapshot.FromEntity(CreateSettings(2, ServerUsage.Photos)));
            ServerSettingsSnapshot second = secondCache.GetOrAdd(
                () => ServerSettingsSnapshot.FromEntity(CreateSettings(6, ServerUsage.Media)));

            Assert.Multiple(() =>
            {
                Assert.That(first.EncryptionThreads, Is.EqualTo(2));
                Assert.That(second.EncryptionThreads, Is.EqualTo(6));
            });
        }

        private static CottonServerSettings CreateSettings(
            int encryptionThreads,
            ServerUsage serverUsage)
        {
            return new CottonServerSettings
            {
                EncryptionThreads = encryptionThreads,
                Timezone = "UTC",
                InstanceId = Guid.NewGuid(),
                PublicBaseUrl = "https://cotton.test",
                ServerUsage = [serverUsage],
            };
        }
    }
}
