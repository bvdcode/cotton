// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Email;
using Cotton.Models.Enums;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Text;
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
                new Dictionary<string, string> { [EmailTemplateParameterNames.Token] = "value" });

            Assert.That(sent, Is.True);
            Assert.That(handler.RequestMethod, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri(CottonPublicEmailProvider.CottonBridgeBaseUrl + "email/send")));
            Assert.That(handler.RequestBody, Is.Not.Null);

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            Assert.That(
                body.RootElement.GetProperty("instanceId").GetGuid(),
                Is.EqualTo(instanceId));
        }

        [Test]
        public async Task SendEmailAsync_MarksPlainTextWithoutChangingValuesOrInputParameters()
        {
            const string content = "<br /> &lt;literal&gt; {{year}}\r\nNext line";
            Dictionary<string, string> parameters = new()
            {
                [EmailTemplateParameterNames.SecurityTitle] = "R&D <Security>",
                [EmailTemplateParameterNames.SecurityContent] = content,
                [EmailTemplateParameterNames.OccurredAt] = "2026-11-16 10:00:00 +00:00 (UTC)",
            };
            using HttpResponseMessage response = new(HttpStatusCode.OK);
            using RecordingHttpMessageHandler handler = new(response);
            using HttpClient client = new(handler)
            {
                BaseAddress = new Uri(CottonPublicEmailProvider.CottonBridgeBaseUrl),
            };
            CottonPublicEmailProvider provider = new(client, NullLogger<CottonPublicEmailProvider>.Instance);

            bool sent = await provider.SendEmailAsync(
                Guid.NewGuid(), EmailTemplate.SecurityAlert, "https://cotton.example",
                "recipient@example.test", "R&D {{year}}", "en", parameters);

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            JsonElement actual = body.RootElement.GetProperty("parameters");
            Assert.Multiple(() =>
            {
                Assert.That(sent, Is.True);
                Assert.That(actual.GetProperty(EmailTemplateParameterNames.Format).GetString(),
                    Is.EqualTo(nameof(EmailParameterFormat.PlainText)));
                Assert.That(actual.GetProperty(EmailTemplateParameterNames.SecurityContent).GetString(), Is.EqualTo(content));
                Assert.That(body.RootElement.GetProperty("recipientName").GetString(), Is.EqualTo("R&D {{year}}"));
                Assert.That(parameters.ContainsKey(EmailTemplateParameterNames.Format), Is.False);
                Assert.That(parameters[EmailTemplateParameterNames.SecurityContent], Is.EqualTo(content));
            });
        }

        [Test]
        public async Task CheckHealthAsync_UsesConfiguredClient()
        {
            using HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"Healthy\"}", Encoding.UTF8, "application/json"),
            };
            using RecordingHttpMessageHandler handler = new(response);
            using HttpClient client = new(handler)
            {
                BaseAddress = new Uri(CottonPublicEmailProvider.CottonBridgeBaseUrl),
            };
            CottonPublicEmailProvider provider = new(
                client,
                NullLogger<CottonPublicEmailProvider>.Instance);

            bool healthy = await provider.CheckHealthAsync();

            Assert.Multiple(() =>
            {
                Assert.That(healthy, Is.True);
                Assert.That(handler.RequestMethod, Is.EqualTo(HttpMethod.Get));
                Assert.That(handler.RequestUri, Is.EqualTo(new Uri(CottonPublicEmailProvider.CottonBridgeBaseUrl + "health")));
            });
        }
    }
}
