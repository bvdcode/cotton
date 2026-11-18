// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Email;
using Cotton.Models.Enums;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class EmailTemplateRendererTests
    {
        [TestCase(EmailTemplate.EmailConfirmation, "en")]
        [TestCase(EmailTemplate.EmailConfirmation, "ru")]
        [TestCase(EmailTemplate.PasswordReset, "en")]
        [TestCase(EmailTemplate.PasswordReset, "ru")]
        public void Render_EncodesRecipientNameAndPreservesTemplateLikeText(EmailTemplate template, string language)
        {
            const string name = "<a href=\"https://example.test\">R&D &lt;Team&gt; {{year}}</a>";
            Dictionary<string, string> variables = EmailTemplateRenderer.BuildVariables(
                name, "recipient@example.test", "test-token", "https://cloud.example.test");

            string html = EmailTemplateRenderer.Render(template, language, variables);

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain(WebUtility.HtmlEncode(name)));
                Assert.That(html, Does.Not.Contain(name));
                Assert.That(variables[EmailTemplateParameterNames.RecipientName], Is.EqualTo(name));
            });
        }

        [TestCase(EmailTemplate.EmailConfirmation, "en", EmailTemplateParameterNames.ConfirmationUrl)]
        [TestCase(EmailTemplate.EmailConfirmation, "ru", EmailTemplateParameterNames.ConfirmationUrl)]
        [TestCase(EmailTemplate.PasswordReset, "en", EmailTemplateParameterNames.ResetUrl)]
        [TestCase(EmailTemplate.PasswordReset, "ru", EmailTemplateParameterNames.ResetUrl)]
        public void Render_EncodesActionUrlsForHtmlAttributes(EmailTemplate template, string language, string parameter)
        {
            const string token = "value&extra=1#fragment%41";
            const string baseUrl = "https://cloud.example.test/path?literal=\"value\"&other=1";
            Dictionary<string, string> variables = EmailTemplateRenderer.BuildVariables(
                "Recipient", "recipient@example.test", token, baseUrl);

            string html = EmailTemplateRenderer.Render(template, language, variables);
            string url = variables[parameter];

            Assert.Multiple(() =>
            {
                Assert.That(url, Does.EndWith("?token=" + Uri.EscapeDataString(token)));
                Assert.That(html, Does.Contain("href=\"" + WebUtility.HtmlEncode(url) + "\""));
                Assert.That(html, Does.Not.Contain("href=\"" + url + "\""));
            });
        }

        [TestCase("\r\n")]
        [TestCase("\n")]
        [TestCase("\r")]
        public void Render_EncodesSecurityFieldsAndFormatsOnlyRealLineBreaks(string lineBreak)
        {
            const string title = "R&D <Security> {{year}}";
            const string timestamp = "Time <UTC> &lt;literal&gt;";
            const string firstLine = "<script>ignored()</script> &lt;literal&gt; {{security_title}}";
            const string secondLine = "Line with <br /> text";
            Dictionary<string, string> variables = EmailTemplateRenderer.BuildVariables(
                "Recipient", "recipient@example.test", string.Empty, "https://cloud.example.test");
            variables[EmailTemplateParameterNames.SecurityTitle] = title;
            variables[EmailTemplateParameterNames.SecurityContent] = firstLine + lineBreak + secondLine;
            variables[EmailTemplateParameterNames.OccurredAt] = timestamp;

            string html = EmailTemplateRenderer.Render(EmailTemplate.SecurityAlert, "en", variables);

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain(WebUtility.HtmlEncode(title)));
                Assert.That(html, Does.Contain(WebUtility.HtmlEncode(timestamp)));
                Assert.That(html, Does.Contain(WebUtility.HtmlEncode(firstLine) + "<br />" + WebUtility.HtmlEncode(secondLine)));
                Assert.That(html, Does.Not.Contain("<script>"));
                Assert.That(html, Does.Contain("href=\"https://cloud.example.test\""));
                Assert.That(variables[EmailTemplateParameterNames.SecurityContent], Is.EqualTo(firstLine + lineBreak + secondLine));
            });
        }

        [Test]
        public void Render_RemovesMissingVariablesWithoutChangingInsertedPlaceholders()
        {
            Dictionary<string, string> variables = new()
            {
                [EmailTemplateParameterNames.RecipientName] = "Team {{token}}"
            };

            string html = EmailTemplateRenderer.Render(EmailTemplate.PasswordReset, "en", variables);

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain("Team {{token}}"));
                Assert.That(html, Does.Not.Contain("{{reset_url}}"));
                Assert.That(html, Does.Not.Contain("{{year}}"));
            });
        }
    }
}
