// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using NUnit.Framework;
using Cotton.Server.Extensions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Services.Bridge;
using Cotton.Server.Services.Computation;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class StartupLifecycleChainTests
    {
        private BridgeTestTransport _bridge = null!;

        private void ConfigureComputationClients(IServiceCollection services)
        {
            _bridge = new BridgeTestTransport();
            services.AddHttpClient(TeiClient.RemoteClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _runner);
            services.AddHttpClient(BridgeCredentialProvider.RegistrationClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _bridge);
            services.AddHttpClient(CottonBridgeServiceCollectionExtensions.ComputationClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _runner);
        }

        [Test]
        public async Task RemoteRunnerValidation_SavesModeAndUrlTogether_AndCachesDimensions()
        {
            SetBearer((await LoginAsync()).AccessToken);
            (await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/remote-computation-runner-url",
                " https://runner.example/proxy/ ")).EnsureSuccessStatusCode();
            JsonElement mode = await GetJsonAsync("/api/v1/server/settings/compution-mode");
            JsonElement url = await GetJsonAsync("/api/v1/server/settings/remote-computation-runner-url");
            JsonElement status = await GetJsonAsync("/api/v1/server/settings/computation-status");
            await GetJsonAsync("/api/v1/server/settings/computation-status");
            Assert.Multiple(() =>
            {
                Assert.That(mode.GetProperty("computionMode").GetString(), Is.EqualTo("Remote"));
                Assert.That(url.GetProperty("remoteComputationRunnerUrl").GetString(), Is.EqualTo("https://runner.example/proxy"));
                Assert.That(status.GetProperty("isReady").GetBoolean(), Is.True);
                Assert.That(status.GetProperty("dimensions").GetInt32(), Is.EqualTo(1024));
                Assert.That(_runner.EmbedCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task FailedRemoteRunnerValidation_PreservesSavedUrlAndMode()
        {
            SetBearer((await LoginAsync()).AccessToken);
            (await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/remote-computation-runner-url", "https://working.example")).EnsureSuccessStatusCode();
            (await _client!.PatchAsync("/api/v1/server/settings/compution-mode/Local", null)).EnsureSuccessStatusCode();
            _runner.Dimensions = 768;
            HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/remote-computation-runner-url", "https://broken.example");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            JsonElement mode = await GetJsonAsync("/api/v1/server/settings/compution-mode");
            JsonElement url = await GetJsonAsync("/api/v1/server/settings/remote-computation-runner-url");
            Assert.Multiple(() =>
            {
                Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("InvalidDimensions"));
                Assert.That(mode.GetProperty("computionMode").GetString(), Is.EqualTo("Local"));
                Assert.That(url.GetProperty("remoteComputationRunnerUrl").GetString(), Is.EqualTo("https://working.example"));
            });
        }

        [Test]
        public async Task CloudValidation_RegistersBridgeAndSavesMode()
        {
            SetBearer((await LoginAsync()).AccessToken);
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/telemetry", true))
                .EnsureSuccessStatusCode();

            HttpResponseMessage response = await _client!.PatchAsync(
                "/api/v1/server/settings/compution-mode/Cloud",
                null);

            response.EnsureSuccessStatusCode();
            JsonElement status = await response.Content.ReadFromJsonAsync<JsonElement>();
            JsonElement mode = await GetJsonAsync("/api/v1/server/settings/compution-mode");
            Assert.Multiple(() =>
            {
                Assert.That(status.GetProperty("isReady").GetBoolean(), Is.True);
                Assert.That(mode.GetProperty("computionMode").GetString(), Is.EqualTo("Cloud"));
                Assert.That(_bridge.Requests, Has.Count.EqualTo(1));
                Assert.That(_runner.InfoCalls, Is.EqualTo(1));
                Assert.That(_runner.EmbedCalls, Is.EqualTo(1));
                Assert.That(_runner.Credentials.All(credential =>
                    credential.Token == _bridge.Requests[0].Token
                    && credential.InstanceId == _bridge.Requests[0].InstanceId), Is.True);
            });
        }

        [Test]
        public async Task FailedCloudValidation_PreservesSavedMode()
        {
            SetBearer((await LoginAsync()).AccessToken);
            (await _client!.PatchAsJsonAsync("/api/v1/server/settings/telemetry", true))
                .EnsureSuccessStatusCode();
            _runner.Dimensions = 768;

            HttpResponseMessage response = await _client!.PatchAsync(
                "/api/v1/server/settings/compution-mode/Cloud",
                null);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            JsonElement mode = await GetJsonAsync("/api/v1/server/settings/compution-mode");
            Assert.That(mode.GetProperty("computionMode").GetString(), Is.EqualTo("Local"));
        }

    }
}
