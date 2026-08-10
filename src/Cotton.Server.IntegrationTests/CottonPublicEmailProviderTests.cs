// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Models.Enums;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests
{
    [TestFixture]
    public class CottonPublicEmailProviderTests
    {
        [Test]
        public async Task SendEmailAsync_UsesProvidedInstanceId()
        {
            Guid instanceId = Guid.NewGuid();
            using HttpResponseMessage response = new(HttpStatusCode.OK);
            using RecordingHttpMessageHandler handler = new(response);
            using HttpClient client = new(handler)
            {
                BaseAddress = new Uri(CottonPublicEmailProvider.CottonBridgeBaseUrl),
            };
            CottonPublicEmailProvider provider = new(
                client,
                NullLogger<CottonPublicEmailProvider>.Instance);

            bool sent = await provider.SendEmailAsync(
                instanceId,
                EmailTemplate.EmailConfirmation,
                "https://cotton.example",
                "recipient@example.com",
                "Recipient",
                "en",
                new Dictionary<string, string> { ["token"] = "value" });

            Assert.That(sent, Is.True);
            Assert.That(handler.RequestMethod, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri(CottonPublicEmailProvider.CottonBridgeBaseUrl + "email/send")));
            Assert.That(handler.RequestBody, Is.Not.Null);

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            Assert.That(
                body.RootElement.GetProperty("instanceId").GetGuid(),
                Is.EqualTo(instanceId));
        }
    }
}
