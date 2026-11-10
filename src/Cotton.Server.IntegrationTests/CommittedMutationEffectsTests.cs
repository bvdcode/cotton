// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Quartz;

namespace Cotton.Server.IntegrationTests
{
    public class CommittedMutationEffectsTests : MoveEndpointTestBase
    {
        [Test]
        public async Task Mutations_SchedulerAndNotificationFailures_PreserveCommittedResults()
        {
            _client!.Dispose();
            await _factory!.DisposeAsync();
            _factory = null;
            using TestAppFactory factory = new(_overrides);
            using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventNotificationService>();
                    services.RemoveAll<ISchedulerFactory>();
                    services.AddSingleton<FailingMutationNotifications>();
                    services.AddSingleton<IEventNotificationService>(provider => provider.GetRequiredService<FailingMutationNotifications>());
                    services.AddSingleton<FailingSchedulerFactory>();
                    services.AddSingleton<ISchedulerFactory>(provider => provider.GetRequiredService<FailingSchedulerFactory>());
                }));
            using HttpClient client = application.CreateClient();
            string token = await LoginViaClientAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto root = (await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeDto folder = await CreateFolderViaClientAsync(client, root.Id, "before");
            HttpResponseMessage renameFolder = await client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/rename", new RenameNodeRequestDto { Name = "after" });
            renameFolder.EnsureSuccessStatusCode();

            const string content = "committed-content";
            NodeFileManifestDto file = await CreateFileViaClientAsync(client, folder.Id, "before.txt", content);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(content)));
            HttpResponseMessage update = await client.PatchAsJsonAsync($"/api/v1/files/{file.Id}/update-content",
                new CreateFileFromChunksRequestDto
                {
                    NodeId = folder.Id, Name = file.Name, Hash = hash, ChunkHashes = [hash], ContentType = "text/plain"
                });
            update.EnsureSuccessStatusCode();
            HttpResponseMessage renameFile = await client.PatchAsJsonAsync($"/api/v1/files/{file.Id}/rename",
                new RenameFileRequestDto { Name = "after.txt" });
            renameFile.EnsureSuccessStatusCode();

            HttpResponseMessage createRoutine = await client.PostAsJsonAsync("/api/v1/routines", new RoutineDraftDto
            {
                Name = "Cleanup", Enabled = true, TargetNodeId = folder.Id, IntervalHours = 24,
                Settings = new() { OlderThanDays = 30 },
            });
            createRoutine.EnsureSuccessStatusCode();
            RoutineDto routine = (await createRoutine.Content.ReadFromJsonAsync<RoutineDto>())!;
            List<RoutineDto> routines = (await client.GetFromJsonAsync<List<RoutineDto>>("/api/v1/routines"))!;
            Assert.That(routines.Select(item => item.Id), Is.EqualTo(new[] { routine.Id }));

            await UseWebDavBasicAuthAsync(client);
            using StringContent putContent = new("webdav-content");
            HttpResponseMessage put = await client.PutAsync("/api/v1/webdav/after/dav.txt", putContent);
            put.EnsureSuccessStatusCode();
            using StringContent replacement = new("webdav-replacement");
            HttpResponseMessage replace = await client.PutAsync("/api/v1/webdav/after/dav.txt", replacement);
            replace.EnsureSuccessStatusCode();
            Assert.That(await client.GetStringAsync("/api/v1/webdav/after/dav.txt"), Is.EqualTo("webdav-replacement"));

            await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.Multiple(() =>
            {
                Assert.That(db.Nodes.AsNoTracking().Single(node => node.Id == folder.Id).Name, Is.EqualTo("after"));
                Assert.That(db.NodeFiles.AsNoTracking().Single(node => node.Id == file.Id).Name, Is.EqualTo("after.txt"));
                Assert.That(db.Routines.AsNoTracking().Single().Id, Is.EqualTo(routine.Id));
                Assert.That(application.Services.GetRequiredService<FailingMutationNotifications>().Attempts, Is.EqualTo(7));
                Assert.That(application.Services.GetRequiredService<FailingSchedulerFactory>().Attempts, Is.EqualTo(15));
            });
        }
    }
}
