// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Controllers;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Quartz;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public partial class VectorExtensionEndpointTests
    {
        [TestCase(null, HttpStatusCode.Unauthorized, 0)]
        [TestCase(nameof(UserRole.User), HttpStatusCode.Forbidden, 0)]
        [TestCase(nameof(UserRole.Admin), HttpStatusCode.Accepted, 1)]
        public async Task TriggerIndexing_RequiresAdministratorAndSchedulesExistingJob(
            string? role, HttpStatusCode expectedStatus, int expectedTriggers)
        {
            await using WebApplication application = await CreateApplicationAsync(DbContext);
            using HttpClient client = application.GetTestClient();
            if (role is not null)
            {
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, role);
            }

            using HttpResponseMessage response = await client.PatchAsync(Routes.V1.Server + "/indexing/trigger", null);

            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
            IScheduler scheduler = await application.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(new JobKey(nameof(GenerateFileEmbeddingsJob)));
            Assert.That(triggers, Has.Count.EqualTo(expectedTriggers));
            Assert.That(triggers.All(trigger => trigger.GetNextFireTimeUtc() <= DateTimeOffset.UtcNow), Is.True);
        }
    }
}
