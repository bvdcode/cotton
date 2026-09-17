// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class StartupLifecycleChainTests
    {
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

    }
}
