// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class ComputationServiceTests
    {
        [Test]
        public async Task Cloud_UsesTheSameProtocolWithBridgeCredentials()
        {
            ConfigureCloud(telemetry: true);
            ComputationStatus status = await _service.GetStatusAsync();
            float[][] fragments = await _service.GetTextEmbeddingFragmentsAsync("Cloud document.");
            Assert.Multiple(() =>
            {
                Assert.That(status.IsReady, Is.True);
                Assert.That(fragments, Has.Length.EqualTo(1));
                Assert.That(_handler.Addresses.All(uri =>
                    new Uri(global::Cotton.Constants.CottonBridgeBaseUrl).IsBaseOf(uri)), Is.True);
                Assert.That(_handler.Addresses.Select(uri => uri.Segments.Last()), Does.Contain("tokenize"));
                Assert.That(_handler.Credentials.All(value => value ==
                    (_credentials.Credential.Token, _credentials.Credential.InstanceId.ToString())), Is.True);
            });
        }

        [Test]
        public async Task Remote_DoesNotReceiveBridgeCredentials()
        {
            await _service.GetStatusAsync();
            await _service.GetTextEmbeddingFragmentsAsync("Private runner document.");
            Assert.That(_handler.Credentials.All(value => value == (null, null)), Is.True);
            Assert.That(_credentials.Calls, Is.Zero);
        }

        [Test]
        public async Task Cloud_WithTelemetryDisabled_DoesNotSendContent()
        {
            ConfigureCloud(telemetry: false);
            ComputationStatus status = await _service.GetStatusAsync();
            Assert.That(status.Error, Is.EqualTo(ComputationError.NotConfigured));
            Assert.That(_handler.Addresses, Is.Empty);
            Assert.That(_credentials.Calls, Is.Zero);
        }

        private void ConfigureCloud(bool telemetry)
        {
            _cache.InvalidateSettings(serverIsInitialized: true);
            _cache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
            {
                ComputionMode = ComputionMode.Cloud,
                TelemetryEnabled = telemetry,
                RemoteComputationRunnerUrl = "https://unused.example/",
            }));
        }
    }
}
