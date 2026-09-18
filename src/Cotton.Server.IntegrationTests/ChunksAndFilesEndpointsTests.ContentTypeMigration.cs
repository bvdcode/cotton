// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore.Migrations;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task ContentTypeMigration_BackfillPopulatesLegacyNamesAcrossBatches_AndIsIdempotent()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto original = await UploadTextFileAsync(root, "original.txt", "Shared legacy content");
            NodeFileManifestDto markdown = await UploadTextFileAsync(root, "document.md", "Shared legacy content");
            Assert.That(original.FileManifestId, Is.EqualTo(markdown.FileManifestId));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile source = await dbContext.NodeFiles.SingleAsync(file => file.Id == original.Id);
            for (int i = 0; i < 1001; i++)
            {
                NodeFile file = new()
                {
                    OwnerId = source.OwnerId,
                    NodeId = source.NodeId,
                    FileManifestId = source.FileManifestId,
                };
                file.SetName($"legacy-{i}.md");
                dbContext.NodeFiles.Add(file);
            }
            await dbContext.SaveChangesAsync();
            foreach (NodeFile file in await dbContext.NodeFiles.AsNoTracking().ToListAsync())
            {
                await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, 1, scope.ServiceProvider);
            }
            var originalDates = await dbContext.NodeFiles.AsNoTracking()
                .Select(file => new { file.Id, file.CreatedAt, file.UpdatedAt }).ToArrayAsync();
            dbContext.ChangeTracker.Clear();

            IMigrator migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync("20260918191923_AddFileTextIndexState");
            await migrator.MigrateAsync();
            Assert.That(await dbContext.NodeFiles.CountAsync(file => file.ContentType == string.Empty),
                Is.EqualTo(originalDates.Length));

            await dbContext.NodeFiles.Where(file => file.Id == original.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.ContentType, "application/custom"));
            FileManifest legacyManifest = await dbContext.FileManifests.SingleAsync(manifest => manifest.Id == original.FileManifestId);
            legacyManifest.ContentType = "application/incorrect";
            await dbContext.SaveChangesAsync();
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, legacyManifest, 1, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();
            HotfixBackfillContentTypeJob job = ActivatorUtilities.CreateInstance<HotfixBackfillContentTypeJob>(scope.ServiceProvider);
            await job.Execute(null!);

            List<NodeFile> updated = await dbContext.NodeFiles.ToListAsync();
            IDatabaseIntegrityVerifier verifier = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            foreach (NodeFile file in updated)
            {
                string expected = file.Id == original.Id
                    ? "application/custom"
                    : FileContentTypeResolver.ResolveFromFileName(file.Name);
                Assert.That(file.ContentType, Is.EqualTo(expected), file.Name);
                verifier.RequireValid(dbContext, file, "content-type-backfill");
                if (file.Id != original.Id)
                {
                    Assert.That(dbContext.Entry(file).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
                }
                var dates = originalDates.Single(originalFile => originalFile.Id == file.Id);
                Assert.That(file.CreatedAt, Is.EqualTo(dates.CreatedAt));
                Assert.That(file.UpdatedAt, Is.EqualTo(dates.UpdatedAt));
            }

            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.That(await mediator.Send(new BackfillNodeFileContentTypesRequest(), CancellationToken.None), Is.Zero);
            FileManifest clearedManifest = await dbContext.FileManifests.SingleAsync(manifest => manifest.Id == original.FileManifestId);
            Assert.That(clearedManifest.ContentType, Is.Empty);
            Assert.That(dbContext.Entry(clearedManifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                Is.EqualTo(FileManifestIntegrityDescriptor.LatestVersion));
            verifier.RequireValid(dbContext, clearedManifest, "test.cleared-manifest");
        }

        [Test]
        public async Task ContentTypeBackfillJob_RepeatsEveryTwelveHours()
        {
            ISchedulerFactory factory = _factory!.Services.GetRequiredService<ISchedulerFactory>();
            IScheduler scheduler = await factory.GetScheduler();
            IReadOnlyCollection<JobKey> keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            foreach (JobKey key in keys)
            {
                IJobDetail? detail = await scheduler.GetJobDetail(key);
                if (detail?.JobType != typeof(HotfixBackfillContentTypeJob))
                {
                    continue;
                }

                IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(key);
                Assert.That(triggers, Has.Count.EqualTo(1));
                Assert.That(triggers.Single(), Is.InstanceOf<ISimpleTrigger>());
                ISimpleTrigger trigger = (ISimpleTrigger)triggers.Single();
                Assert.That(trigger.RepeatCount, Is.EqualTo(SimpleTriggerImpl.RepeatIndefinitely));
                Assert.That(trigger.RepeatInterval, Is.EqualTo(TimeSpan.FromHours(12)));
                return;
            }

            Assert.Fail("Content type backfill job was not registered.");
        }
    }
}
