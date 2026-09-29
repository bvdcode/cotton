// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public class DirectorySortingTests : LayoutFileTestBase
    {
        [Test]
        public async Task Listing_SortsBeforePagingAndPreservesWholeFolderStats()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeDto folder = await CreateNodeAsync(root.Id, "sorted-folder");
            await CreateNodeAsync(folder.Id, "nested");
            await UploadTextFileAsync(folder.Id, "alpha.txt", "123456789");
            await UploadTextFileAsync(folder.Id, "beta.txt", "123");
            await UploadTextFileAsync(folder.Id, "gamma.txt", "123456");
            string route = $"/api/v1/layouts/nodes/{folder.Id}/children";

            NodeContentDto byName = (await _client.GetFromJsonAsync<NodeContentDto>(
                $"{route}?page=2&pageSize=2&sortBy=Name&descending=true"))!;
            NodeContentDto bySize = (await _client.GetFromJsonAsync<NodeContentDto>(
                $"{route}?page=2&pageSize=2&sortBy=SizeBytes&descending=true"))!;
            using HttpResponseMessage filtered = await _client.GetAsync(
                $"{route}?pageSize=1&sortBy=SizeBytes&filterBy=SizeBytes&filterOperator=GreaterThan&filterValue=3&includeStats=true");
            filtered.EnsureSuccessStatusCode();
            NodeContentDto filteredPage = (await filtered.Content.ReadFromJsonAsync<NodeContentDto>())!;
            using HttpResponseMessage named = await _client.GetAsync(
                $"{route}?filterBy=Name&filterOperator=Contains&filterValue=GAMMA");
            named.EnsureSuccessStatusCode();
            NodeContentDto namedPage = (await named.Content.ReadFromJsonAsync<NodeContentDto>())!;
            NodeContentDto names = (await _client.GetFromJsonAsync<NodeContentDto>(
                $"{route}?filterBy=Name&filterOperator=IsAnyOf&filterValues=alpha.txt&filterValues=gamma.txt"))!;
            NodeContentDto sizes = (await _client.GetFromJsonAsync<NodeContentDto>(
                $"{route}?filterBy=SizeBytes&filterOperator=IsAnyOf&filterValues=3&filterValues=6"))!;
            using HttpResponseMessage invalid = await _client.GetAsync(
                $"{route}?filterBy=SizeBytes&filterOperator=GreaterThan&filterValue=invalid");
            Assert.Multiple(() =>
            {
                Assert.That(byName.Files.Select(file => file.Name), Is.EqualTo(new[] { "beta.txt", "alpha.txt" }));
                Assert.That(bySize.Files.Select(file => file.Name), Is.EqualTo(new[] { "gamma.txt", "beta.txt" }));
                Assert.That(filteredPage.Files.Select(file => file.Name), Is.EqualTo(new[] { "gamma.txt" }));
                Assert.That(filtered.Headers.GetValues("X-Total-Count").Single(), Is.EqualTo("2"));
                Assert.That(filteredPage.Stats!.Files, Is.EqualTo(3));
                Assert.That(filteredPage.Stats.Folders, Is.EqualTo(1));
                Assert.That(filteredPage.Stats.SizeBytes, Is.EqualTo(18));
                Assert.That(namedPage.Files.Select(file => file.Name), Is.EqualTo(new[] { "gamma.txt" }));
                Assert.That(names.Files.Select(file => file.Name), Is.EqualTo(new[] { "alpha.txt", "gamma.txt" }));
                Assert.That(sizes.Files.Select(file => file.Name), Is.EqualTo(new[] { "beta.txt", "gamma.txt" }));
                Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            });
        }
    }
}
