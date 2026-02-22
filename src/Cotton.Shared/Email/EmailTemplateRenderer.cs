using Cotton.Models.Enums;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cotton.Email
{
    /// <summary>
    /// Renders email templates with variable substitution.
    /// Templates are stored as compile-time constants in <see cref="EmailTemplates"/>.
    /// Placeholders use <c>{{placeholder}}</c> syntax.
    /// </summary>
    public static class EmailTemplateRenderer
    {
        private static readonly Regex PlaceholderRegex = new Regex(@"\{\{[a-z_]+\}\}", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BuildKey(EmailTemplate.EmailConfirmation, "en")] = EmailTemplates.EmailConfirmationEn,
            [BuildKey(EmailTemplate.EmailConfirmation, "ru")] = EmailTemplates.EmailConfirmationRu,
            [BuildKey(EmailTemplate.PasswordReset, "en")] = EmailTemplates.PasswordResetEn,
            [BuildKey(EmailTemplate.PasswordReset, "ru")] = EmailTemplates.PasswordResetRu,
        };

        private static readonly Dictionary<string, string> Subjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BuildKey(EmailTemplate.EmailConfirmation, "en")] = "Confirm your email \u2014 Cotton Cloud",
            [BuildKey(EmailTemplate.EmailConfirmation, "ru")] = "\u041F\u043E\u0434\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u0435 \u0432\u0430\u0448\u0443 \u043F\u043E\u0447\u0442\u0443 \u2014 Cotton Cloud",
            [BuildKey(EmailTemplate.PasswordReset, "en")] = "Reset your password \u2014 Cotton Cloud",
            [BuildKey(EmailTemplate.PasswordReset, "ru")] = "\u0421\u0431\u0440\u043E\u0441 \u043F\u0430\u0440\u043E\u043B\u044F \u2014 Cotton Cloud",
        };

        /// <summary>
        /// Renders the specified email template with the given variables.
        /// Falls back to English if the requested language template is not available.
        /// </summary>
        /// <param name="template">The email template to render.</param>
        /// <param name="languageCode">ISO 639-1 language code (e.g., "en", "ru").</param>
        /// <param name="variables">Key-value pairs for placeholder substitution.</param>
        /// <returns>The rendered HTML email body.</returns>
        public static string Render(EmailTemplate template, string languageCode, Dictionary<string, string> variables)
        {
            string key = BuildKey(template, languageCode);
            if (!Templates.TryGetValue(key, out string html))
            {
                key = BuildKey(template, "en");
                if (!Templates.TryGetValue(key, out html))
                {
                    throw new InvalidOperationException(
                        "Template '" + template + "' not found for language '" + languageCode + "' and no English fallback.");
                }
            }

            foreach (var kvp in variables)
            {
                html = html.Replace("{{" + kvp.Key + "}}", kvp.Value);
            }

            html = PlaceholderRegex.Replace(html, string.Empty);
            return html;
        }

        /// <summary>
        /// Gets the email subject line for the specified template and language.
        /// Falls back to English if the requested language is not available.
        /// </summary>
        /// <param name="template">The email template type.</param>
        /// <param name="languageCode">ISO 639-1 language code (e.g., "en", "ru").</param>
        /// <returns>The localized subject line.</returns>
        public static string GetSubject(EmailTemplate template, string languageCode)
        {
            if (Subjects.TryGetValue(BuildKey(template, languageCode), out string subject))
            {
                return subject;
            }

            if (Subjects.TryGetValue(BuildKey(template, "en"), out subject))
            {
                return subject;
            }

            return template.ToString();
        }

        /// <summary>
        /// Builds the standard template variables including confirmation and reset URLs.
        /// </summary>
        /// <param name="recipientName">The recipient's display name.</param>
        /// <param name="recipientEmail">The recipient's email address.</param>
        /// <param name="token">The verification or reset token.</param>
        /// <param name="serverBaseUrl">The base URL of the server (e.g., "https://cloud.example.com").</param>
        /// <returns>A dictionary of template variables ready for rendering.</returns>
        public static Dictionary<string, string> BuildVariables(
            string recipientName,
            string recipientEmail,
            string token,
            string serverBaseUrl)
        {
            string baseUrl = serverBaseUrl.TrimEnd('/');
            string escapedToken = Uri.EscapeDataString(token);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["recipient_name"] = recipientName,
                ["recipient_email"] = recipientEmail,
                ["token"] = token,
                ["confirmation_url"] = baseUrl + "/verify-email?token=" + escapedToken,
                ["reset_url"] = baseUrl + "/reset-password?token=" + escapedToken,
                ["year"] = DateTime.UtcNow.Year.ToString(),
            };
        }

        /// <summary>
        /// Maps a <see cref="Language"/> enum value to its ISO 639-1 language code.
        /// </summary>
        /// <param name="language">The language enum value.</param>
        /// <returns>The corresponding ISO 639-1 code.</returns>
        public static string GetLanguageCode(Language language)
        {
            switch (language)
            {
                case Language.Russian:
                    return "ru";
                default:
                    return "en";
            }
        }

        private static string BuildKey(EmailTemplate template, string languageCode)
        {
            return template.ToString() + "." + languageCode;
        }
    }
}
