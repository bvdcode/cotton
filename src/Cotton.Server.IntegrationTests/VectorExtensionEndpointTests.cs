// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Controllers;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using Cotton.Server.Jobs;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quartz;
using System.Net;
using System.Net.Http.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class VectorExtensionEndpointTests : IntegrationTestBase
    {
        private const string Endpoint = Routes.V1.Server + "/database/extensions/vector";

        public VectorExtensionEndpointTests()
            : base($"cotton_vector_status_tests_{Guid.NewGuid():N}")
        {
        }

        [TestCase(null, HttpStatusCode.Unauthorized, 0)]
        [TestCase(nameof(UserRole.User), HttpStatusCode.Forbidden, 0)]
        [TestCase(nameof(UserRole.Admin), HttpStatusCode.Accepted, 1)]
        public async Task Patch_RequiresAdministrator(string? role, HttpStatusCode expectedStatus, int expectedCommands)
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .ReplaceService<IMigrationCommandExecutor, RecordingMigrationCommandExecutor>()
                .Options;
            await using CottonDbContext context = new(options);
            await using WebApplication application = await CreateApplicationAsync(context);
            using HttpClient client = application.GetTestClient();
            if (role is not null)
            {
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, role);
            }

            using HttpResponseMessage response = await client.PatchAsync(Endpoint, null);

            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
            RecordingMigrationCommandExecutor executor =
                (RecordingMigrationCommandExecutor)context.GetService<IMigrationCommandExecutor>();
            Assert.That(executor.Commands, Has.Count.EqualTo(expectedCommands));
        }

        [TestCase(null, HttpStatusCode.Unauthorized)]
        [TestCase(nameof(UserRole.User), HttpStatusCode.Forbidden)]
        public async Task Get_RequiresAdministrator(string? role, HttpStatusCode expectedStatus)
        {
            await using WebApplication application = await CreateApplicationAsync(DbContext);
            using HttpClient client = application.GetTestClient();
            if (role is not null)
            {
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, role);
            }

            using HttpResponseMessage response = await client.GetAsync(Endpoint);

            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        }

        [Test]
        public async Task Get_ReturnsStatusAndCountsFragmentsWithoutEnablingExtension()
        {
            try
            {
                await DbContext.Database.EnsureCreatedAsync();
                await using WebApplication application = await CreateApplicationAsync(DbContext);
                using HttpClient client = application.GetTestClient();
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, nameof(UserRole.Admin));

                VectorExtensionStatusDto? emptyStatus = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);

                Assert.That(emptyStatus, Is.Not.Null);
                Assert.That(emptyStatus!.ExtensionEnabled, Is.False);
                Assert.That(emptyStatus.VectorCount, Is.Zero);
                Assert.That(emptyStatus.FileCount, Is.Zero);
                Assert.That(emptyStatus.EmbeddedFileCount, Is.Zero);
                Assert.That(emptyStatus.IndexReady, Is.False);
                Assert.That(emptyStatus.IndexBuilding, Is.False);
                Assert.That(emptyStatus.IndexSizeBytes, Is.Zero);
                Assert.That(emptyStatus.DatabaseName, Is.EqualTo(CurrentDatabaseName));
                Assert.That(emptyStatus.PostgresMajorVersion, Is.GreaterThanOrEqualTo(13));

                FileManifest manifest = new()
                {
                    ProposedContentHash = [1, 2, 3],
                    ContentType = "text/plain",
                    SizeBytes = 3
                };
                DbContext.FileEmbeddings.AddRange(
                    new FileEmbedding
                    {
                        FileManifest = manifest,
                        FragmentIndex = 0,
                        IndexVersion = 1,
                        Embedding = [1, 0]
                    },
                    new FileEmbedding
                    {
                        FileManifest = manifest,
                        FragmentIndex = 1,
                        IndexVersion = 1,
                        Embedding = [0, 1]
                    });
                await DbContext.SaveChangesAsync();

                VectorExtensionStatusDto? populatedStatus = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);

                Assert.That(populatedStatus, Is.Not.Null);
                Assert.That(populatedStatus!.ExtensionEnabled, Is.False);
                Assert.That(populatedStatus.VectorCount, Is.EqualTo(2));
                Assert.That(populatedStatus.ExtensionAvailable, Is.EqualTo(emptyStatus.ExtensionAvailable));
            }
            finally
            {
                await DbContext.Database.EnsureDeletedAsync();
            }
        }

        [Test]
        public async Task Get_CountsCurrentFilesWithCurrentEmbeddingsWithoutCountingFragmentsOrHistory()
        {
            try
            {
                await DbContext.Database.EnsureCreatedAsync();
                User user = new() { Username = "vectorcounts", PasswordPhc = "phc", WebDavTokenPhc = "webdav" };
                Layout layout = new() { Owner = user, IsActive = true };
                Node folder = new() { Owner = user, Layout = layout, Type = NodeType.Default };
                folder.SetName("files");
                Node trash = new() { Owner = user, Layout = layout, Type = NodeType.Trash };
                trash.SetName("trash");
                Node inactive = new() { Owner = user, Layout = new Layout { Owner = user }, Type = NodeType.Default };
                inactive.SetName("inactive");
                FileManifest indexed = new() { ProposedContentHash = [1], ContentType = "text/plain" };
                FileManifest outdated = new() { ProposedContentHash = [2], ContentType = "text/plain" };
                FileManifest pending = new() { ProposedContentHash = [3], ContentType = "application/octet-stream" };
                FileManifest orphaned = new() { ProposedContentHash = [4], ContentType = "text/plain" };
                NodeFile current = AddFile("first.txt", folder, indexed, user);
                current.OriginalNodeFileId = current.Id;
                AddFile("copy.txt", folder, indexed, user);
                AddFile("old-index.txt", folder, outdated, user);
                AddFile("pending.bin", folder, pending, user);
                AddFile("history.txt", folder, indexed, user).OriginalNodeFileId = current.Id;
                AddFile("deleted.txt", trash, indexed, user);
                AddFile("inactive.txt", inactive, indexed, user);
                DbContext.FileEmbeddings.AddRange(
                    new FileEmbedding { FileManifest = indexed, IndexVersion = VectorIndexDefinition.Version, FragmentIndex = 0, Embedding = [1] },
                    new FileEmbedding { FileManifest = indexed, IndexVersion = VectorIndexDefinition.Version, FragmentIndex = 1, Embedding = [1] },
                    new FileEmbedding { FileManifest = outdated, IndexVersion = VectorIndexDefinition.Version - 1, Embedding = [1] },
                    new FileEmbedding { FileManifest = orphaned, IndexVersion = VectorIndexDefinition.Version, Embedding = [1] });
                await DbContext.SaveChangesAsync();
                await using WebApplication application = await CreateApplicationAsync(DbContext);
                using HttpClient client = application.GetTestClient();
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, nameof(UserRole.Admin));

                VectorExtensionStatusDto? status = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);

                Assert.That(status!.FileCount, Is.EqualTo(4));
                Assert.That(status.EmbeddedFileCount, Is.EqualTo(2));
                Assert.That(status.VectorCount, Is.EqualTo(4));
            }
            finally
            {
                await DbContext.Database.EnsureDeletedAsync();
            }
        }

        private NodeFile AddFile(string name, Node folder, FileManifest manifest, User user)
        {
            NodeFile file = new() { Node = folder, FileManifest = manifest, Owner = user };
            file.SetName(name);
            DbContext.NodeFiles.Add(file);
            return file;
        }

        [Test]
        public async Task Get_ReportsDueIndexBuildTriggersAndIgnoresFutureOrPausedTriggers()
        {
            try
            {
                await DbContext.Database.EnsureCreatedAsync();
                await using WebApplication application = await CreateApplicationAsync(DbContext);
                using HttpClient client = application.GetTestClient();
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, nameof(UserRole.Admin));
                IScheduler scheduler = await application.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
                JobKey jobKey = new(nameof(BuildVectorIndexJob));
                await scheduler.ScheduleJob(TriggerBuilder.Create()
                    .ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.AddDays(1))
                    .Build());

                VectorExtensionStatusDto? future = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);
                Assert.That(future!.IndexBuilding, Is.False);

                await scheduler.TriggerJob(jobKey);
                VectorExtensionStatusDto? queued = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);
                Assert.That(queued!.IndexBuilding, Is.True);

                await scheduler.PauseJob(jobKey);
                VectorExtensionStatusDto? paused = await client.GetFromJsonAsync<VectorExtensionStatusDto>(Endpoint);
                Assert.That(paused!.IndexBuilding, Is.False);
            }
            finally
            {
                await DbContext.Database.EnsureDeletedAsync();
            }
        }

        private static async Task<WebApplication> CreateApplicationAsync(CottonDbContext context,
            IRequestHandler<BuildVectorIndexRequest, string?>? indexBuilder = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "Cotton.Server",
                EnvironmentName = "Testing"
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(context);
            builder.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            builder.Services.AddMediator();
            if (indexBuilder is not null)
            {
                builder.Services.AddSingleton(indexBuilder);
            }
            builder.Services.AddQuartz(options =>
            {
                options.SchedulerName = $"VectorEndpointTests-{Guid.NewGuid():N}";
                options.AddJob<BuildVectorIndexJob>(job => job.WithIdentity(nameof(BuildVectorIndexJob)).StoreDurably());
                options.AddJob<GenerateFileEmbeddingsJob>(job => job.WithIdentity(nameof(GenerateFileEmbeddingsJob))
                    .DisallowConcurrentExecution().StoreDurably());
            });
            builder.Services.AddTransient<IRequestHandler<EnsureVectorExtensionRequest>, EnsureVectorExtensionRequestHandler>();
            builder.Services.AddTransient<IRequestHandler<GetVectorExtensionStatusQuery, VectorExtensionStatusDto>, GetVectorExtensionStatusQueryHandler>();
            builder.Services.AddTransient<IRequestHandler<GetVectorIndexMetadataQuery, PostgresIndexStatus>, GetVectorIndexMetadataQueryHandler>();
            builder.Services.AddAuthentication(VectorEndpointTestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, VectorEndpointTestAuthenticationHandler>(
                    VectorEndpointTestAuthenticationHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddControllersAsServices();
            builder.Services.AddTransient(provider => new ServerController(
                provider.GetRequiredService<IMediator>(), null!, provider.GetRequiredService<ISchedulerFactory>(), null!));

            WebApplication application = builder.Build();
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapControllers();
            await application.StartAsync();
            return application;
        }
    }
}
