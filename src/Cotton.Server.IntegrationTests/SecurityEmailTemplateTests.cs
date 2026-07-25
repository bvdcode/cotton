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
                ["security_title"] = "Password changed",
                ["security_content"] = "Your password was changed.",
                ["occurred_at"] = "2026-07-11 09:00:00",
                ["server_url"] = "https://cloud.example.test",
                ["year"] = "2026"
            };

            string body = EmailTemplateRenderer.Render(EmailTemplate.SecurityAlert, "en", variables);

            Assert.Multiple(() =>
            {
                Assert.That(
                    EmailTemplateRenderer.GetSubject(EmailTemplate.SecurityAlert, "en"),
                    Is.EqualTo("Security alert \u2014 Cotton Cloud"));
                Assert.That(body, Does.Contain("Password changed"));
                Assert.That(body, Does.Contain("Your password was changed."));
                Assert.That(body, Does.Not.Contain("{{"));
            });
        }
    }
}
