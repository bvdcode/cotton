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
