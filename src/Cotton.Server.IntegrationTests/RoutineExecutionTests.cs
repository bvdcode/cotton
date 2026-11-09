// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Routines;
using Cotton.Server.Handlers.Routines;
using EasyExtensions.Mediator;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class RoutineExecutionTests : FileEndpointTestBase
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task CleanupRespectsAgeSubfoldersAndDeletionMode(bool recursive, bool permanent)
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "cleanup");
            NodeDto nested = await CreateFolderAsync(folder.Id, "nested");
            NodeFileManifestDto old = await UploadTextFileAsync(folder, "old.txt", "old");
            NodeFileManifestDto child = await UploadTextFileAsync(nested, "child.txt", "child");
            NodeFileManifestDto outside = await UploadTextFileAsync(root, "outside.txt", "outside");
            NodeFileManifestDto fresh = await UploadTextFileAsync(folder, "fresh.txt", "fresh");
            await AgeAsync(old.Id, child.Id, outside.Id);
            RoutineDto routine = await CreateAsync(folder.Id, recursive, permanent);
            await RunJobAsync();
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            RoutineRun run = await db.RoutineRuns.SingleAsync(x => x.RoutineId == routine.Id);
            Assert.That(run.Status, Is.EqualTo(RoutineRunStatus.Completed));
            Assert.That(run.DeletedCount, Is.EqualTo(recursive ? 2 : 1));
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == fresh.Id && x.NodeId == folder.Id), Is.True);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == outside.Id && x.NodeId == root.Id), Is.True);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == old.Id), Is.EqualTo(!permanent));
            if (!permanent)
            {
                Assert.That((await db.NodeFiles.Include(x => x.Node).SingleAsync(x => x.Id == old.Id)).Node.Type, Is.EqualTo(NodeType.Trash));
            }
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == child.Id && x.NodeId == nested.Id), Is.EqualTo(!recursive));
            await RunJobAsync();
            await RunJobAsync(routine.Id);
            Assert.That(await db.RoutineRuns.CountAsync(x => x.RoutineId == routine.Id), Is.EqualTo(1));
        }

        [Test]
        public async Task ModifiedDateAndDelayedStartAreRespectedAndRunNowOverridesStart()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto old = await UploadTextFileAsync(root, "old.txt", "old");
            await AgeAsync(old.Id);
            RoutineDraftDto draft = Draft(root.Id);
            draft.Settings.DateField = RoutineDateField.UpdatedAt;
            draft.StartAtLocal = DateTime.UtcNow.AddDays(7);
            RoutineDto routine = await PostAsync(draft);
            await RunJobAsync();
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await db.RoutineRuns.AnyAsync(), Is.False);
            (await _client!.PostAsync($"/api/v1/routines/{routine.Id}/run", null)).EnsureSuccessStatusCode();
            Assert.That((await db.Routines.AsNoTracking().SingleAsync()).LastExecutionAt, Is.Null);
            await RunJobAsync(routine.Id);
            Assert.That((await db.RoutineRuns.SingleAsync()).DeletedCount, Is.Zero);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == old.Id && x.NodeId == root.Id), Is.True);
        }

        [Test]
        public async Task MissingFolderFailsSafelyAndRoutineCanBeDeletedWithHistory()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "cleanup");
            RoutineDto routine = await CreateAsync(folder.Id, false, false);
            (await _client!.PostAsJsonAsync("/api/v1/items/delete", new BatchItemsRequestDto
            {
                Items = [new BatchItemRequestDto { Id = folder.Id, Kind = BatchItemKind.Folder }], SkipTrash = true
            })).EnsureSuccessStatusCode();
            await RunJobAsync();
            List<RoutineRunDto>? runs = await _client!.GetFromJsonAsync<List<RoutineRunDto>>($"/api/v1/routines/{routine.Id}/runs");
            Assert.That(runs!.Single().Error, Is.EqualTo("WorkingFolderUnavailable"));
            (await _client.DeleteAsync($"/api/v1/routines/{routine.Id}")).EnsureSuccessStatusCode();
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await db.Routines.AnyAsync(), Is.False);
            Assert.That(await db.RoutineRuns.AnyAsync(), Is.False);
        }

        [Test]
        public async Task CannotCreateRoutineForAnotherOwnerOrReadTheirHistory()
        {
            NodeDto root = await AuthenticateAsync();
            Guid foreign;
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                User other = new() { Username = "other-routine-owner", PasswordPhc = "test", WebDavTokenPhc = "test" };
                db.Users.Add(other);
                Routine routine = new() { Owner = other, Name = "private", Settings = JsonSerializer.Serialize(Draft(root.Id).Settings) };
                db.Routines.Add(routine);
                await db.SaveChangesAsync();
                foreign = routine.Id;
            }
            Assert.That((await _client!.GetAsync($"/api/v1/routines/{foreign}/runs")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await _client.PostAsync($"/api/v1/routines/{foreign}/run", null)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await _client.PutAsJsonAsync($"/api/v1/routines/{foreign}", Draft(root.Id))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await _client.DeleteAsync($"/api/v1/routines/{foreign}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await _client.PostAsJsonAsync("/api/v1/routines", Draft(Guid.NewGuid()))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(await _client.GetFromJsonAsync<List<RoutineDto>>("/api/v1/routines"), Is.Empty);
        }

        [Test]
        public async Task ConditionalDeletionRechecksLocationAndAgeInsideTheMutationGate()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "cleanup");
            NodeFileManifestDto old = await UploadTextFileAsync(folder, "old.txt", "old");
            NodeFileManifestDto outside = await UploadTextFileAsync(root, "outside.txt", "outside");
            NodeFileManifestDto fresh = await UploadTextFileAsync(folder, "fresh.txt", "fresh");
            await AgeAsync(old.Id, outside.Id);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid owner = await db.Nodes.Where(x => x.Id == root.Id).Select(x => x.OwnerId).SingleAsync();
            List<BatchItemRequestDto> items = new[] { old, outside, fresh }
                .Select(x => new BatchItemRequestDto { Id = x.Id, Kind = BatchItemKind.File }).ToList();
            IReadOnlyList<BatchItemResultDto> result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new Cotton.Server.Handlers.Files.DeleteBatchItemsRequest(owner, items, true,
                    new Cotton.Server.Models.FileDeletionCondition(folder.Id, false, RoutineDateField.CreatedAt, DateTime.UtcNow.AddDays(-30))));
            Assert.That(result.Single(x => x.Id == old.Id).Deleted, Is.True);
            Assert.That(result.Where(x => x.Id != old.Id).All(x => !x.Deleted), Is.True);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == outside.Id), Is.True);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == fresh.Id), Is.True);
        }

        private async Task<NodeDto> AuthenticateAsync()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            return (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
        }

        private async Task AgeAsync(params Guid[] ids)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            foreach (NodeFile file in await db.NodeFiles.Where(x => ids.Contains(x.Id)).ToListAsync())
            {
                db.Entry(file).Property(x => x.CreatedAt).CurrentValue = DateTime.UtcNow.AddDays(-60);
            }
            await db.SaveChangesAsync();
        }

        private static RoutineDraftDto Draft(Guid folderId) => new()
        {
            Name = "Cleanup", Enabled = true, TargetNodeId = folderId, IntervalHours = 24,
            Settings = new() { OlderThanDays = 30, DateField = RoutineDateField.CreatedAt },
        };

        private Task<RoutineDto> CreateAsync(Guid folderId, bool recursive, bool permanent)
        {
            RoutineDraftDto draft = Draft(folderId);
            draft.Settings.IncludeSubfolders = recursive;
            draft.Settings.PermanentlyDelete = permanent;
            return PostAsync(draft);
        }

        private async Task<RoutineDto> PostAsync(RoutineDraftDto draft)
        {
            HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/routines", draft);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<RoutineDto>())!;
        }

        private async Task RunJobAsync(Guid? requested = null)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RunDueRoutinesRequest(requested));
        }
    }
}
