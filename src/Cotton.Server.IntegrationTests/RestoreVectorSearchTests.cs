// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Mediator;
using Quartz;

namespace Cotton.Server.IntegrationTests
{
    public class RestoreVectorSearchTests() : IntegrationTestBase($"cotton_vector_restore_tests_{Guid.NewGuid():N}")
    {
        private ServiceProvider _services = null!;

        [SetUp]
        public async Task SetUp()
        {
            await DbContext.Database.EnsureCreatedAsync();
            _services = VectorRestoreTestServices.Create(DbContext);
        }

        [TearDown]
        public async Task TearDown()
        {
            await DbContext.Database.EnsureDeletedAsync();
            await _services.DisposeAsync();
        }

        [Test]
        public async Task Services_WhenPreviousLoggerIsDisposed_CanCreateScheduler()
        {
            ILoggerFactory previousLogger = LoggerFactory.Create(_ => { });
            Quartz.Logging.LogContext.SetCurrentLogProvider(previousLogger);
            previousLogger.Dispose();
            await using ServiceProvider services = VectorRestoreTestServices.Create(DbContext);
            IScheduler scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            Assert.That(await scheduler.CheckExists(new JobKey(nameof(BuildVectorIndexJob))), Is.True);
            await scheduler.Shutdown();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task Handle_RestoresPreviousConfiguration(bool hadExtension, bool hadIndex)
        {
            bool available = await DbContext.Database.IsExtensionAvailableAsync(VectorIndexDefinition.ExtensionName);
            IMediator mediator = _services.GetRequiredService<IMediator>();
            VectorSearchRestoreStatus result = await mediator.Send(new RestoreVectorSearchRequest(new(hadExtension, hadIndex)));
            VectorSearchRestoreStatus expected = (hadExtension, available) switch
            {
                (false, false) or (false, true) => VectorSearchRestoreStatus.NotEnabled,
                (true, false) => VectorSearchRestoreStatus.PackageMissing,
                (true, true) => VectorSearchRestoreStatus.Enabled
            };
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(await DbContext.Database.IsExtensionInstalledAsync(VectorIndexDefinition.ExtensionName), Is.EqualTo(hadExtension && available));
            IScheduler scheduler = await _services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(new JobKey(nameof(BuildVectorIndexJob)));
            Assert.That(triggers.Count, Is.EqualTo(hadIndex && available ? 1 : 0));
            if (hadIndex && available)
            {
                Assert.That(await mediator.Send(new BuildVectorIndexRequest()), Is.Null);
                Assert.That(VectorIndexDefinition.IsReady(await mediator.Send(new GetVectorIndexMetadataQuery())), Is.True);
            }
        }
    }
}
