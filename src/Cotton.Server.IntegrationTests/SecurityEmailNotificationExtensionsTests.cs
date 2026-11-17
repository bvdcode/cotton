// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Email;
using Cotton.Server.Extensions;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class SecurityEmailNotificationExtensionsTests : IntegrationTestBase
    {
        [SetUp]
        public async Task SetUp()
        {
            await RecreateDatabaseAsync();
        }

        [Test]
        public async Task SendSecurityEmailAsync_PreservesPlainTextUntilRendering()
        {
            const string title = "R&D <Security> {{year}}";
            const string content = "Literal &lt;value&gt; and <br />\r\nNext line";
            DateTime occurredAt = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
            RecordingNotificationsProvider notifications = new();
            SettingsProvider settings = new(DbContext, new ServerSettingsCache());

            await notifications.SendSecurityEmailAsync(
                settings, NullLogger.Instance, Guid.NewGuid(), title, content, occurredAt);

            var (_, template, parameters, serverBaseUrl, _) = notifications.Emails.Single();
            Assert.Multiple(() =>
            {
                Assert.That(template, Is.EqualTo(EmailTemplate.SecurityAlert));
                Assert.That(parameters[EmailTemplateParameterNames.SecurityTitle], Is.EqualTo(title));
                Assert.That(parameters[EmailTemplateParameterNames.SecurityContent], Is.EqualTo(content));
                Assert.That(parameters[EmailTemplateParameterNames.OccurredAt],
                    Is.EqualTo(SecurityEmailTimestampFormatter.Format(occurredAt, TimeZoneInfo.Utc)));
                Assert.That(serverBaseUrl, Is.EqualTo("http://localhost"));
            });
        }
    }
}
