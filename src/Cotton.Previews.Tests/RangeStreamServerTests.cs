// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews.Http;
using Cotton.Previews.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;

namespace Cotton.Previews.Tests
{
    public class RangeStreamServerTests
    {
        [TestCase("bytes=-3")]
        [TestCase("bytes=7-")]
        [TestCase("bytes=7-20")]
        public async Task Get_WithSatisfiableRange_ReturnsResolvedBytes(string range)
        {
            (HttpStatusCode StatusCode, string? ContentRange, byte[] Content) response =
                await SendRangeAsync(range).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(response.ContentRange, Is.EqualTo("bytes 7-9/10"));
                Assert.That(response.Content, Is.EqualTo(new byte[] { 7, 8, 9 }));
            });
        }

        [TestCase("items=0-1")]
        [TestCase("bytes=0-1,4-5")]
        public async Task Get_WithUnsupportedRange_ReturnsRequestedRangeNotSatisfiable(string range)
        {
            (HttpStatusCode StatusCode, string? ContentRange, byte[] Content) response =
                await SendRangeAsync(range).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestedRangeNotSatisfiable));
                Assert.That(response.ContentRange, Is.Null);
                Assert.That(response.Content, Is.Empty);
            });
        }

        [Test]
        public async Task Get_WithRangeStartingPastEnd_ReturnsStreamLength()
        {
            (HttpStatusCode StatusCode, string? ContentRange, byte[] Content) response =
                await SendRangeAsync("bytes=10-").ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestedRangeNotSatisfiable));
                Assert.That(response.ContentRange, Is.EqualTo("bytes */10"));
                Assert.That(response.Content, Is.Empty);
            });
        }

        [Test]
        public async Task DisposeAsync_WaitsForActiveRangeRequestBeforeDisposingSemaphore()
        {
            BlockingReadStream stream = new(length: 1024 * 1024);
            CapturingLogger logger = new();
            RangeStreamServer server = new(stream, logger);

            using HttpClient client = new();
            using HttpRequestMessage request = new(HttpMethod.Get, server.Url);
            request.Headers.Range = new RangeHeaderValue(0, 1023);

            Task<HttpResponseMessage> requestTask = client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            await stream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await server.DisposeAsync().ConfigureAwait(false);
            await ObserveRequestCompletionAsync(requestTask).ConfigureAwait(false);

            IReadOnlyList<(LogLevel Level, Exception? Exception, string Message)> entries = logger.Entries;
            (LogLevel Level, Exception? Exception, string Message)[] errorEntries =
                entries.Where(entry => entry.Level >= LogLevel.Error).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(errorEntries.Any(IsSemaphoreDisposedEntry), Is.False);
                Assert.That(
                    errorEntries,
                    Is.Empty,
                    string.Join(Environment.NewLine, errorEntries.Select(entry => $"{entry.Level}: {entry.Message}")));
            });
        }

        [Test]
        public async Task DisposeAsync_ReturnsWhenActiveReadIgnoresCancellation()
        {
            BlockingReadStream stream = new(length: 1024 * 1024, observeCancellation: false);
            CapturingLogger logger = new();
            RangeStreamServer server = new(
                stream,
                logger,
                requestDrainTimeout: TimeSpan.FromMilliseconds(100));

            using HttpClient client = new();
            using HttpRequestMessage request = new(HttpMethod.Get, server.Url);
            request.Headers.Range = new RangeHeaderValue(0, 1023);

            Task<HttpResponseMessage> requestTask = client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            await stream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Assert.That(
                logger.Entries.Any(entry => entry.Level == LogLevel.Warning
                    && entry.Message.Contains("Timed out waiting", StringComparison.Ordinal)),
                Is.True);

            _ = ObserveRequestCompletionAsync(requestTask);
        }

        [TestCase(64)]
        [TestCase(995)]
        [TestCase(10053)]
        [TestCase(10054)]
        [TestCase(10058)]
        public void IsExpectedClientDisconnect_KnownSocketErrors_ReturnsTrue(int errorCode)
        {
            HttpListenerException exception = new(errorCode, "Client disconnected");

            Assert.That(RangeStreamServer.IsExpectedClientDisconnect(exception), Is.True);
        }

        [Test]
        public void IsExpectedClientDisconnect_DisposedSocketMessage_ReturnsTrue()
        {
            HttpListenerException exception = new(
                1,
                "Unable to write data: Cannot access a disposed object. Object name: 'System.Net.Sockets.SafeSocketHandle'.");

            Assert.That(RangeStreamServer.IsExpectedClientDisconnect(exception), Is.True);
        }

        private static async Task ObserveRequestCompletionAsync(Task<HttpResponseMessage> requestTask)
        {
            try
            {
                using HttpResponseMessage response = await requestTask
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
            }
        }

        private static async Task<(HttpStatusCode StatusCode, string? ContentRange, byte[] Content)> SendRangeAsync(
            string range)
        {
            byte[] source = Enumerable.Range(0, 10).Select(value => (byte)value).ToArray();
            using MemoryStream stream = new(source, writable: false);
            await using RangeStreamServer server = new(stream);
            using HttpClient client = new();
            using HttpRequestMessage request = new(HttpMethod.Get, server.Url);
            request.Headers.TryAddWithoutValidation("Range", range);

            using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            byte[] content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            return (response.StatusCode, response.Content.Headers.ContentRange?.ToString(), content);
        }

        private static bool IsSemaphoreDisposedEntry((LogLevel Level, Exception? Exception, string Message) entry)
        {
            return entry.Exception is ObjectDisposedException objectDisposedException
                   && string.Equals(
                       objectDisposedException.ObjectName,
                       typeof(SemaphoreSlim).FullName,
                       StringComparison.Ordinal);
        }
    }
}
