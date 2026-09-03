// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class MoveEndpointsTests
    {
        [Test]
        public async Task WebDavMove_NotificationFailureDoesNotFailRequest()
        {
            // Reset the standard factory so we can wire a throwing notifier.
            _client?.Dispose();
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }

            using TestAppFactory factory = new TestAppFactory(_overrides);
            using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventNotificationService>();
                    services.AddScoped<IEventNotificationService, ThrowingMoveEventNotificationService>();
                });
            });
            using HttpClient client = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            // Provision the user + source/destination folders + a file via REST first.
            string token = await LoginViaClientAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto? root = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            NodeDto src = await CreateFolderViaClientAsync(client, root!.Id, "src");
            NodeDto dst = await CreateFolderViaClientAsync(client, root.Id, "dst");
            NodeFileManifestDto file = await CreateFileViaClientAsync(client, src.Id, "doc.txt", "webdav-fail-notify");

            // Switch to WebDAV basic auth for the MOVE request.
            await UseWebDavBasicAuthAsync(client);

            using HttpRequestMessage moveRequest = new HttpRequestMessage(new HttpMethod("MOVE"), "/api/v1/webdav/src/doc.txt");
            moveRequest.Headers.Add("Destination", "/api/v1/webdav/dst/doc.txt");
            moveRequest.Headers.Add("Overwrite", "F");
            HttpResponseMessage res = await client.SendAsync(moveRequest);

            // WebDAV MOVE returns 201 Created when the destination did not previously exist,
            // or 204 NoContent on overwrite. Either is success — but it MUST NOT fail
            // because the realtime notifier threw after the move already committed.
            Assert.That((int)res.StatusCode, Is.AnyOf(201, 204),
                $"WebDAV MOVE must succeed despite notification failure (got {(int)res.StatusCode}).");

            using (IServiceScope scope = customFactory.Services.CreateScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeFile moved = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == file.Id);
                Assert.That(moved.NodeId, Is.EqualTo(dst.Id), "File must have been moved despite notification failure.");
            }
        }

        [Test]
        public async Task WebDavDelete_NotificationsUseOriginalParents()
        {
            _client?.Dispose();
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }

            using TestAppFactory factory = new TestAppFactory(_overrides);
            using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventNotificationService>();
                    services.AddSingleton<WebDavDeleteEventRecorder>();
                    services.AddScoped<IEventNotificationService, RecordingWebDavDeleteEventNotificationService>();
                });
            });
            using HttpClient client = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            string token = await LoginViaClientAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            NodeDto? root = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            NodeDto fileParent = await CreateFolderViaClientAsync(client, root!.Id, "delete-file-parent");
            NodeFileManifestDto file = await CreateFileViaClientAsync(client, fileParent.Id, "doc.txt", "webdav-delete-file");
            NodeDto folder = await CreateFolderViaClientAsync(client, root.Id, "delete-folder-parent");

            await UseWebDavBasicAuthAsync(client);

            using HttpRequestMessage deleteFileRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/webdav/delete-file-parent/doc.txt");
            HttpResponseMessage deleteFileResponse = await client.SendAsync(deleteFileRequest);
            Assert.That(deleteFileResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            using HttpRequestMessage deleteFolderRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/webdav/delete-folder-parent");
            HttpResponseMessage deleteFolderResponse = await client.SendAsync(deleteFolderRequest);
            Assert.That(deleteFolderResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            WebDavDeleteEventRecorder recorder = customFactory.Services.GetRequiredService<WebDavDeleteEventRecorder>();
            Assert.Multiple(() =>
            {
                Assert.That(recorder.FileDeletedCount, Is.EqualTo(1));
                Assert.That(recorder.FileDeletedNodeFileId, Is.EqualTo(file.Id));
                Assert.That(recorder.FileDeletedParentNodeId, Is.EqualTo(fileParent.Id));
                Assert.That(recorder.NodeDeletedCount, Is.EqualTo(1));
                Assert.That(recorder.NodeDeletedNodeId, Is.EqualTo(folder.Id));
                Assert.That(recorder.NodeDeletedParentNodeId, Is.EqualTo(root.Id));
            });
        }

        [Test]
        public async Task MoveFile_NotificationFailureDoesNotFailRequest()
        {
            // Reset the standard factory so we can wire a throwing notifier.
            _client?.Dispose();
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }

            using TestAppFactory factory = new TestAppFactory(_overrides);
            using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventNotificationService>();
                    services.AddScoped<IEventNotificationService, ThrowingMoveEventNotificationService>();
                });
            });
            using HttpClient client = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            // Authenticate via this client.
            string token = await LoginViaClientAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto src = await CreateFolderViaClientAsync(client, root!.Id, "src");
            NodeDto dst = await CreateFolderViaClientAsync(client, root.Id, "dst");
            NodeFileManifestDto file = await CreateFileViaClientAsync(client, src.Id, "doc.txt", "fail-notify-content");

            HttpResponseMessage res = await client.PatchAsJsonAsync(
                $"/api/v1/files/{file.Id}/move",
                new MoveFileRequestDto { ParentId = dst.Id });

            // The handler must catch the notifier exception and still return 200.
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Notification failure must not turn a committed move into a failed response.");

            // Verify the move actually happened in DB.
            await using CottonDbContext db = NewReadOnlyDbContext();
            NodeFile moved = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == file.Id);
            Assert.That(moved.NodeId, Is.EqualTo(dst.Id));
        }

    }
}
