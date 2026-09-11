// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Cotton.Sdk.Tests
{
    public class CottonFileContentClientTests
    {
        private const string IfMatchHeaderName = "If-Match";

        [Test]
        public async Task DownloadContentAsync_CopiesResponseBodyAndReportsProgress()
        {
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("downloaded")),
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);
            using MemoryStream destination = new MemoryStream();
            RecordingProgress progress = new RecordingProgress();

            await client.Files.DownloadContentAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), destination, progress: progress);

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(destination.ToArray()), Is.EqualTo("downloaded"));
                Assert.That(progress.Values.Last(), Is.EqualTo(10));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/content?download=false"));
            });
        }

        [Test]
        public async Task DownloadContentRangeAsync_SendsRangeAndIfMatchAndCopiesPartialBody()
        {
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(_ =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("4567")),
                };
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(4, 7, 16);
                response.Headers.ETag = new EntityTagHeaderValue("\"sha256-current\"");
                response.Headers.AcceptRanges.Add("bytes");
                return response;
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);
            using MemoryStream destination = new MemoryStream();
            RecordingProgress progress = new RecordingProgress();

            await client.Files.DownloadContentRangeAsync(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                destination,
                offset: 4,
                length: 4,
                expectedETag: "sha256-current",
                progress: progress);

            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(destination.ToArray()), Is.EqualTo("4567"));
                Assert.That(progress.Values.Last(), Is.EqualTo(4));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/content?download=false"));
                Assert.That(handler.Requests[0].Headers["Range"], Is.EqualTo("bytes=4-7"));
                Assert.That(handler.Requests[0].Headers[IfMatchHeaderName], Is.EqualTo("\"sha256-current\""));
            });
        }

        [Test]
        public async Task DownloadContentRangeAsync_RejectsUnexpectedSuccessfulFullResponse()
        {
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("0123456789abcdef")),
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);
            using MemoryStream destination = new MemoryStream();

            CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(async () =>
                await client.Files.DownloadContentRangeAsync(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    destination,
                    offset: 4,
                    length: 4));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(exception.Message, Does.Contain("unexpected status 200"));
                Assert.That(destination.Length, Is.Zero);
            });
        }

        [Test]
        public async Task DownloadContentRangeAsync_RejectsChunkedPartialResponseWithExtraBytes()
        {
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(_ =>
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("4567x")),
                };
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(4, 7, 16);
                response.Content.Headers.ContentLength = null;
                return response;
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);
            using MemoryStream destination = new MemoryStream();

            CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(async () =>
                await client.Files.DownloadContentRangeAsync(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    destination,
                    offset: 4,
                    length: 4));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Message, Does.Contain("more bytes than expected"));
                Assert.That(Encoding.UTF8.GetString(destination.ToArray()), Is.EqualTo("4567"));
            });
        }

        [Test]
        public async Task DownloadContentRangeAsync_ValidatesArguments()
        {
            CottonCloudClient client = await CreateAuthorizedClientAsync(new QueuedHttpMessageHandler());
            using MemoryStream destination = new MemoryStream();

            Assert.Multiple(() =>
            {
                Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                    await client.Files.DownloadContentRangeAsync(Guid.NewGuid(), destination, offset: -1, length: 1));
                Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                    await client.Files.DownloadContentRangeAsync(Guid.NewGuid(), destination, offset: 0, length: 0));
                Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                    await client.Files.DownloadContentRangeAsync(Guid.NewGuid(), destination, long.MaxValue, length: 2));
            });
        }

        private static async Task<CottonCloudClient> CreateAuthorizedClientAsync(QueuedHttpMessageHandler handler)
        {
            InMemoryCottonTokenStore store = new InMemoryCottonTokenStore();
            await store.SaveAsync(new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" });
            return new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });
        }

        private class RecordingProgress : IProgress<long>
        {
            public List<long> Values { get; } = [];

            public void Report(long value)
            {
                Values.Add(value);
            }
        }
    }
}
