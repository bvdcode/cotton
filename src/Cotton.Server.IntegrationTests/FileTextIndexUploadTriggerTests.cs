// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Quartz;

namespace Cotton.Server.IntegrationTests
{
    public class FileTextIndexUploadTriggerTests : FileEndpointTestBase
    {
        [Test]
        public async Task CreateFile_SchedulesPreviewAndTextIndexingAfterCommit()
        {
            NodeDto root = await AuthenticateAsync();
            Dictionary<JobKey, int> before = await GetTriggerCountsAsync();

            NodeFileManifestDto file = await UploadTextFileAsync(root, "created.txt", "Created content");

            await AssertScheduledAsync(before);
            Assert.That(await DbContext.NodeFiles.AnyAsync(nodeFile => nodeFile.Id == file.Id), Is.True);
        }

        [Test]
        public async Task RejectedCreate_DoesNotSchedulePreviewOrTextIndexing()
        {
            NodeDto root = await AuthenticateAsync();
            const string content = "Existing content";
            await UploadTextFileAsync(root, "existing.txt", content);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(content)));
            Dictionary<JobKey, int> before = await GetTriggerCountsAsync();

            using HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    NodeId = root.Id,
                    Name = "existing.txt",
                    ContentType = "text/plain",
                    ChunkHashes = [hash],
                    Hash = hash,
                });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Dictionary<JobKey, int> after = await GetTriggerCountsAsync();
            foreach ((JobKey job, int count) in before)
            {
                Assert.That(after[job], Is.EqualTo(count));
            }
        }

        [Test]
        public async Task ReplaceContent_SchedulesPreviewAndTextIndexingAfterCommit()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto original = await UploadTextFileAsync(root, "updated.txt", "Original content");
            Dictionary<JobKey, int> before = await GetTriggerCountsAsync();

            NodeFileManifestDto updated = await UpdateTextFileAsync(original, root, "Updated content");

            await AssertScheduledAsync(before);
            Assert.That(updated.FileManifestId, Is.Not.EqualTo(original.FileManifestId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task WebDavPut_SchedulesPreviewAndTextIndexingAfterCommit(bool replace)
        {
            NodeDto root = await AuthenticateAsync();
            if (replace)
            {
                await UploadTextFileAsync(root, "webdav.txt", "Original content");
            }
            string token = await GetWebDavTokenAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{token}")));
            Dictionary<JobKey, int> before = await GetTriggerCountsAsync();

            using StringContent content = new("WebDAV content");
            using HttpResponseMessage response = await _client.PutAsync("/api/v1/webdav/webdav.txt", content);

            response.EnsureSuccessStatusCode();
            await AssertScheduledAsync(before);
        }

        private async Task<NodeDto> AuthenticateAsync()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver")
                ?? throw new InvalidOperationException("Root folder was not returned.");
        }

        private async Task<Dictionary<JobKey, int>> GetTriggerCountsAsync()
        {
            IScheduler scheduler = await _factory!.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            Dictionary<JobKey, int> counts = [];
            JobKey[] jobs = [new(nameof(GeneratePreviewJob)), new(nameof(GenerateFileEmbeddingsJob))];
            foreach (JobKey job in jobs)
            {
                counts[job] = (await scheduler.GetTriggersOfJob(job)).Count;
            }
            return counts;
        }

        private async Task AssertScheduledAsync(Dictionary<JobKey, int> before)
        {
            Dictionary<JobKey, int> after = await GetTriggerCountsAsync();
            foreach ((JobKey job, int count) in before)
            {
                Assert.That(after[job], Is.EqualTo(count + 1), $"Scheduled trigger count for {job.Name}");
            }
        }
    }
}
