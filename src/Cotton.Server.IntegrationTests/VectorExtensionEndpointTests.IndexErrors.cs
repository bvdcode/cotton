// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System.Net.Http.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class VectorExtensionEndpointTests
    {
        [TestCase(PostgresErrorCodes.InsufficientPrivilege, "pgvector_index_permission_denied")]
        [TestCase(PostgresErrorCodes.InternalError, "pgvector_index_build_failed")]
        [TestCase(null, "pgvector_index_incompatible")]
        public async Task Get_ReportsLastBuildFailureWithoutAnIndexAndClearsItAfterSuccess(string? sqlState, string expectedError)
        {
            bool fail = true;
            VectorIndexBuildTestHandler handler = new(() =>
            {
                if (!fail)
                {
                    return null;
                }
                if (sqlState is not null)
                {
                    throw new PostgresException("Index creation failed", "ERROR", "ERROR", sqlState);
                }
                return expectedError;
            });
            await using WebApplication application = await CreateApplicationAsync(DbContext, handler);
            BuildVectorIndexJob job = new(application.Services.GetRequiredService<IMediator>(), NullLogger<BuildVectorIndexJob>.Instance);
            IScheduler scheduler = await application.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            IJobDetail detail = JobBuilder.Create<BuildVectorIndexJob>().Build();
            TriggerFiredBundle bundle = new(detail, (IOperableTrigger)TriggerBuilder.Create().Build(),
                null, false, DateTimeOffset.UtcNow, null, null, null);
            using JobExecutionContextImpl context = new(scheduler, bundle, job);
            try
            {
                await DbContext.Database.EnsureCreatedAsync();
                if (sqlState is not null)
                {
                    Assert.ThrowsAsync<PostgresException>(() => job.Execute(context));
                }
                else
                {
                    await job.Execute(context);
                }
                using HttpClient client = application.GetTestClient();
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, nameof(UserRole.Admin));
                VectorExtensionStatusDto? failed = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);
                Assert.That(failed!.IndexReady, Is.False);
                Assert.That(failed.IndexSizeBytes, Is.Zero);
                Assert.That(failed.IndexErrorCode, Is.EqualTo(expectedError));

                fail = false;
                await job.Execute(context);
                VectorExtensionStatusDto? retried = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);
                Assert.That(retried!.IndexErrorCode, Is.Null);
            }
            finally
            {
                fail = false;
                await job.Execute(context);
                await DbContext.Database.EnsureDeletedAsync();
            }
        }
    }
}
