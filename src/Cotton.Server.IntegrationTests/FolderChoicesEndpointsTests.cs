// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class FolderChoicesEndpointsTests : FileEndpointTestBase
    {
        [Test]
        public async Task PickerPagesOnlyOwnedActiveFoldersAndRejectsInvalidPages()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            await UploadTextFileAsync(root, "not-a-folder.txt", "content");
            Guid foreignId;
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Node parent = await db.Nodes.SingleAsync(x => x.Id == root.Id);
                for (int index = 0; index < 201; index++)
                {
                    db.Nodes.Add(parent.CopyTo(parent, $"folder-{index:D3}"));
                }
                User other = new() { Username = "folder-picker-owner", PasswordPhc = "test", WebDavTokenPhc = "test" };
                Layout layout = new() { Owner = other, IsActive = true };
                Node foreign = new() { Owner = other, Layout = layout };
                foreign.SetName("private");
                db.Nodes.Add(foreign);
                await db.SaveChangesAsync();
                foreignId = foreign.Id;
            }

            FolderChoicesDto first = (await _client.GetFromJsonAsync<FolderChoicesDto>($"/api/v1/layouts/nodes/{root.Id}/folders"))!;
            FolderChoicesDto second = (await _client.GetFromJsonAsync<FolderChoicesDto>($"/api/v1/layouts/nodes/{root.Id}/folders?page=2"))!;
            Assert.Multiple(() =>
            {
                Assert.That(first.Current.Id, Is.EqualTo(root.Id));
                Assert.That(first.Folders, Has.Count.EqualTo(200));
                Assert.That(first.HasMore, Is.True);
                Assert.That(second.Folders.Select(x => x.Name), Is.EqualTo(new[] { "folder-200" }));
                Assert.That(second.HasMore, Is.False);
                Assert.That(first.Folders.Select(x => x.Id).Intersect(second.Folders.Select(x => x.Id)), Is.Empty);
            });
            Assert.That((await _client.GetAsync($"/api/v1/layouts/nodes/{foreignId}/folders")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await _client.GetAsync($"/api/v1/layouts/nodes/{root.Id}/folders?page=0")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await _client.GetAsync($"/api/v1/layouts/nodes/{root.Id}/folders?page={int.MaxValue}")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }
}
