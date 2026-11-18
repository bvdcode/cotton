// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Security.Cryptography;

namespace Cotton.Storage.Tests.Helpers
{
    internal class S3RetryTestHandler(byte[] expectedHash, long expectedLength) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Multiple(() =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Put));
                Assert.That(request.Content!.Headers.ContentLength, Is.EqualTo(expectedLength));
                Assert.That(request.Headers.TransferEncodingChunked, Is.Not.True);
                Assert.That(request.Headers.GetValues("x-amz-content-sha256").Single(),
                    Is.EqualTo(Convert.ToHexStringLower(expectedHash)));
                Assert.That(request.Headers.GetValues("Authorization").Single(), Does.StartWith("AWS4-HMAC-SHA256 "));
            });
            Stream body = await request.Content!.ReadAsStreamAsync(cancellationToken);
            Assert.That(await SHA256.HashDataAsync(body, cancellationToken), Is.EqualTo(expectedHash));
            Attempts++;
            if (Attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("<Error><Code>InternalError</Code><Message>Retry</Message></Error>"),
                };
            }
            HttpResponseMessage response = new(HttpStatusCode.OK);
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"stored\"");
            return response;
        }
    }
}
