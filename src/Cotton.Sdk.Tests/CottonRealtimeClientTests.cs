// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk.Realtime;

namespace Cotton.Sdk.Tests
{

    public sealed class CottonRealtimeClientTests
    {
        [Test]
        public void CreateUri_UsesApiV1EventHubRoute()
        {
            Uri uri = CottonRealtimeHubEndpoint.CreateUri(new Uri("https://app.cottoncloud.dev"));

            Assert.That(uri, Is.EqualTo(new Uri("https://app.cottoncloud.dev/api/v1/hub/events")));
        }

        [Test]
        public void CreateUri_PreservesBaseAddressPath()
        {
            Uri uri = CottonRealtimeHubEndpoint.CreateUri(new Uri("https://app.cottoncloud.dev/cloud"));

            Assert.That(uri, Is.EqualTo(new Uri("https://app.cottoncloud.dev/cloud/api/v1/hub/events")));
        }

        [Test]
        public void RemoteFileTreeChanged_IncludesServerMutationMethods()
        {
            Assert.That(
                CottonRealtimeHubMethods.RemoteFileTreeChanged,
                Is.EqualTo(new[]
                {
                    "FileCreated",
                    "FileUpdated",
                    "FileDeleted",
                    "FileMoved",
                    "FileRenamed",
                    "FileRestored",
                    "NodeCreated",
                    "NodeDeleted",
                    "NodeMoved",
                    "NodeRenamed",
                    "NodeRestored",
                }));
        }

        [Test]
        public void CottonRealtimeEvent_RejectsUnknownKind()
        {
            Assert.That(
                () => new CottonRealtimeEvent(CottonRealtimeEventKind.Unknown, "FileCreated", DateTime.UtcNow),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
