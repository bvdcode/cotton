// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Email;
using Cotton.Models.Enums;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class SecurityEmailTemplateTests
    {
        [Test]
        public void SecurityAlertTemplate_RendersSubjectAndBody()
        {
            Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase)
            {
                [EmailTemplateParameterNames.SecurityTitle] = "Password changed",
                [EmailTemplateParameterNames.SecurityContent] = "Your password was changed.",
                [EmailTemplateParameterNames.OccurredAt] = "2026-07-11 09:00:00",
                [EmailTemplateParameterNames.ServerUrl] = "https://cloud.example.test",
                [EmailTemplateParameterNames.Year] = "2026"
            };

            string body = EmailTemplateRenderer.Render(EmailTemplate.SecurityAlert, "en", variables);

            Assert.Multiple(() =>
            {
                Assert.That(
                    EmailTemplateRenderer.GetSubject(EmailTemplate.SecurityAlert, "en"),
                    Is.EqualTo("Security alert \u2014 Cotton Cloud"));
                Assert.That(body, Does.Contain("Password changed"));
                Assert.That(body, Does.Contain("Your password was changed."));
                Assert.That(body, Does.Contain("https://cottoncloud.dev/favicon-96x96.png"));
                Assert.That(body, Does.Contain("background:#151A21"));
                Assert.That(body, Does.Contain("background:#96be02"));
                Assert.That(body, Does.Not.Contain("cid:"));
                Assert.That(body, Does.Not.Contain("linear-gradient"));
                Assert.That(body, Does.Not.Contain("{{"));
            });
        }
    }
}
