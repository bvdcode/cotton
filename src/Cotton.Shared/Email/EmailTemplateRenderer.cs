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
        /// <summary>
        /// Content-ID used for the inline logo attachment in email templates.
        /// Templates reference this as <c>cid:cotton-logo</c>.
        /// </summary>
        public const string IconContentId = "cotton-logo";

        /// <summary>
        /// MIME type of the inline logo attachment.
        /// </summary>
        public const string IconContentType = "image/png";
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
        public static string GetLanguageCode(Language language) =>
            language switch
            {
                Language.Russian => "ru",
                _ => "en",
            };

        private static string BuildKey(EmailTemplate template, string languageCode)
        {
            return template.ToString() + "." + languageCode;
        }

        /// <summary>
        /// Returns the raw PNG bytes of the inline logo icon.
        /// Used by email senders to attach the logo as a CID-referenced inline image.
        /// </summary>
        public static byte[] GetIconBytes()
        {
            return Convert.FromBase64String(IconBase64);
        }

        /// <summary>
        /// Creates the standard inline attachments list containing the Cotton logo.
        /// Used by both SMTP and cloud (Mailjet) email senders.
        /// </summary>
        public static IReadOnlyList<InlineAttachment> GetInlineAttachments()
        {
            return new[]
            {
                new InlineAttachment(IconContentId, IconContentType, "cotton-logo.png", GetIconBytes()),
            };
        }

        #region Icon Base64

        private const string IconBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAMAAAADACAYAAABS3GwHAAAQAElEQVR4AexdBXwURxf/310SKMUJgba4" +
            "FmmLtbRI8eIuBQpUKNbi7m5Bire4ywfF4YPi7q7FNWggUJyQ3H3v5UsoSS53ezu7l5OZ38ytzNP/vHe3" +
            "OytnhCx6IWD6OB8yNemJRhO3Yuaauzi2/SUe7HqDV3stCKNmpmaJbLwexn1EE8y0zNO4BxqzDDLQRE1W" +
            "HRCQCaAdqAlqt0HVBWexcXc4XlBgh808hqsth2BegVL4MVUa5PNLiFQmHyQglSZqBmpRlddN3Ec0/kzL" +
            "PK2GYi7LYFkk8+Wis9hctx2qERPLoIWsogjIBBBA0N8fSfrMRd8drxBCQfqq03iszpwL5YxGvCcg1ior" +
            "yUyYMRfKdBiLVayLdfadh34psyGpVQa5UxECMgEUwRSNyFCvHWpveYY7q4PxpGJjDPBNgBTRKJywwTor" +
            "NEL/tRfxz1ay5YfuqEtq+ZeEFrIqRUAmgFKkAN/BizGGvn3D2o/F0vfeR1rlrPpSJiRbmg/DErZtyBKM" +
            "BdlKTVYFCBgV0HgoiWK3fOiEdAYFV2jpemgPwJUxM5aqi3ZsK9tMtvpSk9UGAq48mDbMdk5XvznoycFE" +
            "J6Q/OUejdlrYZrL9df/Z6K2dVM+TJBPAypgWqYR8u8PwrHwTDKFuAzV3rYZvvscg8uV58Soo4K5O6Gm3" +
            "TIDo6Bqm78eKUf/FMaMJ70fvct8t8iVR4BocmXMEq8gLd05oMl/bKhMgEs+8RZCV5tpf5C6MGvDQkr0A" +
            "qu0x40WBksjmoS467JZMAIKs83i0nboHl3iunTY9uhoMSDhxGy52moB2Hu2oQue8PgEWnMLqWm0wTiFeH" +
            "kNWuzXGzj+FtR7jkEpHvDkBjOvu42LmvKiqEju3Z8uSF5UZA3LEa+PAWx333f4S95OnRjYafK+ujAFhEU" +
            "wg+FLzuuqNCeC3JwwP/BIildeNdhwOExYpd4XiIXX7UfOq6m0JYNr+AvcNpvi5gcxiQdir5wi5dwN/37y" +
            "IHUGXsP7mBfxF6zvv38A57mOa+IhAky+SMDak20TNY2tMx7wpAQz0U3/D7z0kiwmCTtuW4Fs4uXAk2jb" +
            "+BBmKGGAqaoRv6cRIVTMjcn+bAyXrZUelb3OiIq2XqJERubiPaZiWeeaNRGuWAcBCTffK2DBGpMhAzSuq" +
            "1yTAypvYRz/1H+o8qpYbZ7GhVWnkpSA2Vk+HzyZ2xYTLp3GT9JqpKa1m5vmjKyaxDJb169fIc/k41pMA" +
            "XZOBMVpNWJEer6hekQCBqzEuIB0K6zWi5nA8n9wbP1CgmurnQYUT23BGa13HduFs4/yoxDqm9ML3rFNrH" +
            "VHy/AmrEWvAd5XC04vHJ0CVpihTvCra6jGQFjNeDW+K6sV8kHjuEMwhHbp+O5N8rpY5QzGXdQ5rjqpkw" +
            "0veqXUrVgXtGDut5bqaPKOrGaSlPR9+iEQ9p2OTljKjZK2agX5FTXhv9Uysjtrn7OWaaVhLNiRaNQ199N" +
            "DN2DGGesh2FZkenQAzT+IkAR15QkdrGtSXT3G9clYkD/wZAzUQp4mIwOYYXCUVkj19hGvQthgiMdRWqgt" +
            "J89gE6DgarZKmQlYtsd65AqPKJEWmR1fwj5ZytZAVEoIn5VMi886VGKGFvCgZjGHzoWgVte1pS6OnORTh" +
            "TyYkrNMRkyLWNfoY1gxlutdCF43E6Same010G9oMpbRU8EMPTEI6vKelTFeR5ZEJsGRjxHG/Voc+5l/LIN" +
            "Oa6djqKoNmz46107G9TTFkJDpHpl6JPM5qWLIVG+PsdeMOj0uAz4ohR7rsKKbRmITXyo60x7biukbynCb" +
            "myB7cqJMTaUhhODXhypjmK4zswoJcTIDHJcCYDdiiEcaWhpmR7u4l8I1iGol0rpjbF/Dguyz4CBpdSf5tq" +
            "/v8CipF2qMSgJ97TZgI6ZQ6b4uuTTl8du0a7tqicYe+q1dxr20pfKqFrYwtY6yFLFeR4VEJMGAxVmsB7K" +
            "xBaHpkM05pIcsVZBzejtNzBuMnLWwhjPm5Yi1EuYQMj0mAfF8iE31DfSSK6vW/sXFaX8wUleNq/FP6YNa" +
            "1s9ggahdhnC5XkYgTbFFRLsHvMQnQbQ7miSJqsSC0QW5UEJXjqvwN86Ai+fha1L4h8zFfVIar8HtKAvhk" +
            "zAHhmR+a5y9OA+OM+3lITbxUS886YB+FlKfNHIG1j5AQJzHbU+MRCdB8EJrZc9Re/50bOLhrJQ7ao3P3/" +
            "h3LcejedRwQ9aPFQPwsKsMV+D0iARp2xgBRMLuUwjeiMtyFv2kBcV8bdMFAd/HXlp1unwB8t6JfQqS25a" +
            "S9vnNHsPaKC97fY89utf1839C5o1itlp/5GPN0HnB7hJGdcedWpSUaitrfqR4aicpwN/7O5dBY1OYKLcSx" +
            "F7VBlN/tE6BWK3QWASHkPs644t2dIj4p4eVfgcf3cVoJbVw01X+Gy98cGJftUfvdPgGS+iNHlDPRl8q2A" +
            "lvgJ2WUnkcV2ErM91RpxbB3BUTdOgEy5UZaAlHkrk+zN8z8EEZWK88IUYfIzXKGyDEgMe5Z3TEBjAx6jW" +
            "ao2GsmhB7cpiujWt04B3ct549hs4jtPAY1m6FKxlz4gOS4XTy5vME8y9OwPWrNP4VN/EcPey0IX3gGd7pO" +
            "xbo8hfEtga66zhuF4aqZPYRx3lAITWfyGHSZijWLzuI2jw2P0YKT2NKkK+rw2Lk6TC6ZAPxtMnItJu18g" +
            "2dLb+F56zFYliUvyvIfPWgJ6PpZ2K2lPHeUtXUphC+Kves3j1HmT1C6ZSD+5LGjhHjGY5k+J/R+J9O7Zi" +
            "hed6UE8OsxAz0ZMP42KVoZv/j44H3FnjhIaA7HK2IJpeZ51TGPwsPD8dwxFuXUlBDv81guPodbNLbPeYyJ" +
            "24+aS9R4TwA+nl9yEVvp5/N11Z8whAFzBjJ3b0BoCtAZNjpLR9AlHHWGLhrbRDzGPNY05ttyfw6exHCG6j" +
            "h1xFsC5MyPD/97Fyf4eD5dNpSK00KdOo7vhPCtwTqZ5nSxx3fgL2crpTEvOf0g7lAMnORYcLb+KH1OTwA" +
            "+MaLs3z7rKG6lSANNnlSCinJqD3apYPNIlpO7sSe+HKMY+IRjgWOCY8PZdjg1AfrOQR8+MaLsL+FsR2Pq" +
            "u3oCF2Lu89btm+dwJb5955jg2OgzF32daYtTEiBbYaTb+RoPKzQRm3LTEpibFxGipTx3lnX7MlzmRV8VG2" +
            "MAxUoIx4waTB3l0T0B+sxBz7n7cdPHDykdNU5P+kepwLNAeqpwG9khIa6FBcVKCo4Zjh29QdQzAUz/vYf" +
            "jFZtgiN5OqJJ/CeGq+DyTyeyKbnHsrLuHE2SbiZouVZcEyPopAnaH42mKAHymi9XaCNXFd21Mc7oUl8Uie" +
            "QA+3UOxxDGlByqaO16qLgrOO4G7RiNc+l2S/v5IoAeg7igzZUokdGW7DRRLHFMla6OQ1nZqmgBNuqDKkC" +
            "U4TEYaqLl0DciIFC5toBON+zArkjlRnVpVhqFLcYhjTK0Aa3yaJUDj7qjbcgTWWFPinH2OaXlyD277ykPH" +
            "PLVPffoQ3OYNeBxjHGv2vVJGoUkCNO6Gqq2GYYkylbpQWcLD8PLlMwQ/DsalkAc48ygYF54+wl3a/4w0Rj" +
            "vJWzASLYKCoMtfC5Eud6xv5o/EjzEMNxN2z58/xT3C8mIUpv88xB3aHwvTGLy6bnKsccxpoUQ4AUrXwBe" +
            "thkPoAWuHHLEg/O41HJo/Am2aF0W2Igb4UTMW90WiMkkQUCkA2aukRt7KAchZPiU+oP1JqN9Ezad8Ovjzc" +
            "lJXTIUs0RD4vStmMzaNsyENLX2pmQi7xOWSIi1hmSMK04r++JD2v8W0yWdIP2MAmgVdwF5YEBZNqI4bHHM" +
            "ce6IqjCICchXEB4NXYL+IDCW8FgveHNyEyQx2ESN8amXGF793w8TTe3EZwBtqSmr401t4SIRy+pNAiKOGX" +
            "76M+9SnNJDDL51E0Iz+mF4vJ4rS2Pg2zIMPti7FeIsZwm+gIztsVo49jkGbRHY6RRLAZ/ohXCX5up3wPn" +
            "2MS73q4MuiRvi1/watGGzSJ6sLI3DtLO72rot2RU1I2Lky8tMh6d86mmuIjEEftTpUJ8C6YJw2GJBArWJb" +
            "fI/u4Rx/25dPgezbluGALVrZB8BFQdi7DsfpkDR3g9z4MPgWTuphJsfgf4NxSq1soxrG/oswJLk/cqrhtcV" +
            "jDsezrjVRqHJa5JLf9raQcq++63/jTvV0+Kx9BeSjE+gnWlufwh8fc0xCRXE4AfIWQdZv6qOnCl02Wfaux" +
            "4RiPkiyeyWO2CSUnW6LwMENOEEn0Ml2LMdorZ3gmOTYdFSu0VGGyTtxHFoWC8LalscnnSuhrZZipSzXRaBH" +
            "bXRuUwq5YFE8gaHImd+3w+HYdCgBRq7CaKMJiaFRefEUt2jm4P3DGyEfT9QIU3cRc2Q7zvHYP/8HN7Sy2c" +
            "cXiTlGHZGnOAEyZULyotXQ0RHhtmhvX8WBskmRDoB8MJ1A8NL6plxyZKRYUP1EWkzcOEazZIHiWzsUJ8CE" +
            "vdgWU5nabXJ4S50s+FItv+TzLAQoFooFXcUGrbwat1t5rCpKgE+LImeqD5APGpQ7V7GPHC6rgSgpwoMQqJc" +
            "FFSg2dmvhEsVq/s+KIYcSWYoSIHCVNtn5/Alu1s6CIkoMkzTehwDFRvEXT3ANGpThK5XFrN0EyFUY2ZOlQ" +
            "kZhmyx4Uy4ZsgrLkQI8GoGyyZAdFgifF1LMZuLYtQeW3QQYvRrL7AlR0t++HPISndL7dohUVi9FIKx5MeTW" +
            "wneK3aX25NhMAH9/JEkegE8gWPasxLCDW6Dxa0gEjZLsLosA3+T411wMEjWQYvdTjmFbcmwmQN8FCLTFrKQ" +
            "vLBSPutSE5leOleiWNO6LwMDv0fdNKIRfXdN1hu0YtpkAhb5BM1EIW1fAV6IyJL93ItC2jHjsFKtmO4bjTI" +
            "BCJSOO2VXfZspDdusKDpzchvO8LptEwFEETuzGhVuXsc9Rvhj0PpGxHGP3/zfjTIBfRkH48KdDVVT6vxr5K" +
            "RFQh0Cn0qisjvNfrl9HYfi/W9HX4kyAjwuifHRSx7buXsPBoLPix3COafUSai9y88YNPOIjCRGXcxZEhbj4" +
            "rSYAv7OfGEzUVNduddFANbN3MBo/L4d83Mhdq+NA+2UlBHo3EI4lU2RMk7To1SrwdX6J9YaA6Fx2tsLD8P" +
            "TiYVyxQ+a13Z+XQp69dLFn3EYc48brto5TvRaoSMfPH8RViimhB2kopr+PFBdtYTUBSn8rlgCrp2JoNC1y" +
            "4y0C1Zuh7LitEbd/v/sLaxq/Dae47y2hXImGwKppYjFV5lv8FE1g5IbVBEjuD6FbFmb3x8RI+XLxDgJVm6" +
            "FKt6nY9M6uaKvcxzTRdsqNCATm9MOkiBWVH8n8kc0aa6wEiPyXjlj7rTFb20c/Vc+Dg8EvTrLW7bX7qv6M" +
            "0j2mYo09AJiGae3ReVs/xxTHloDfxsjYjiYiVqBnLgSh+zDOHcK6aBrkBgp8jVw9nlB93gAAEABJREFUpm" +
            "GLUiiYlnmU0nsLnWhs5bAS27ES4MtyKC0C6JJJmCLC72m8KbMh6cQdOO2oX8zjnxNJHOXzZHrR2MpfFrH+" +
            "jDFWAuT+AsVFQDy+EfI9Pv8CaFh5Bnx/eyycYb8Yl5/EdSIzUJOVEBCNrTyFY8d2rIFJmwm5SJfaauFjNb" +
            "XMnsY36zCW89/9qPWLeWeQDLX8nsYXGVsWtX5Zi+1YCZA0JVKrVWA2Q/7vViR45RuheM6CqAHBkotksCxBM" +
            "R7DLhJjyVIiICYQsRLA5INEMYmUbr94Ch3fua/UCpegM/Sdi81aWRIpy6CVPHeWIxJj1mI7VgIQONb20W77" +
            "NTgI/GZh+4QeTjF0KUYYDPDTyk2WxTK1kufOch7dFfqSNcb0PdYOIlD9TUM/Tw+I39urX8na6Kw1CJEyNUs" +
            "qre1zlrw3byASY7FiW9MEMBrkOcDvOzFZr2DQU7ZeNmsuVyzGFCWAapvDAaEHaFQrdh1GU77iiPlXQ5pZR7J" +
            "/IGHv3kNEm95VLRZtY8xoBT6zlX2KdlF6JVdE6KFEPaehuy6u/SvUQFeJu/276X1rgjEWK7atJYDqedY06ZH" +
            "W+4bkX48rN0Xff7f0WavSFP30keweUgPSI42ApbFiO1YChIdB9Vx+wkRCxgn4Ff+sJaric56t0dsS1lG0Ggr" +
            "prcdV5YvEmLXYjpUANM/6WK3zPr54H15auk6D0/55sucU5+lyteEUiTFrsR0rAe4H4bKA0yzPG6fqfFKkgSY" +
            "vD4aCkiIt8gPwxgkHji0j+a6qUmxfiskYS9j5I9gXk8iR7S/KC91L5Igql6Ft3ANOf/45PnTGB+Dv6iwmGFs" +
            "XrMR2rAQ4uAWK71t/17io9Urfo0nUurcsG3XBYGf72rgLhF8d6GybRfWV+x6NRWQc3IZYsR0rAc5uxxERJV" +
            "9Xw3ci/G7I65MkBTI42+7EKcBv7PaqawLFBWPrzFYcjTlOsRIgKAiPYhI5sp3wffA0ldccn5apB6HnJyBQy" +
            "taPP90CZqtl9aHYEppmtxbbsRKArLO8fgWhuzp/6IFqJMcrKh3+tI4vR2v9ijbxpdvZeuu1RhURnZExbf86A" +
            "Cs5sg0reKm2NeyK0Wp53Y0vyydij5CK+Jvnc8R6xE9EnivzNh0oFlOHt2K5Nf+s/QJg2W8Qeq1J4uTIZO+9" +
            "7LBb3IPANwGSIZ4K6faKW09Sp0ZiOs/KIgLz4nHWY9pqAuzbjNMiypi33xJM4KWHN56XNsSjj6ybbYhHE/RX" +
            "3W8xxolqObwRZ6zJsJoARGgJuW+dgfoU1YKl0IQInXkybOL3vqRLh5QBmZEmTVYEZMiAFGRDAmocKLTQtpIe" +
            "lq+tUAel6WgDY5aAMWQsSU8axpYxJhOdOftkKlAKQnfYRsZyrON/8gNxJQBmD0QvJhBohklb9P0VaNQVNTY+" +
            "wc29FpiphS29hedLbuLhyiu4u+IS7v3nOkJo/ytq5r1mhG1+gnszD2H1d93QKCsliIBvEawZsiN1xEo8fmTMA" +
            "X9R9YwFnbc1ZmwYI8YqAjMLXjGGjCVjytgyxtQXRs3M2DegMRDVb4t/7DqMBcDJCLVlziDE+Q9FcSbA0kmw+" +
            "xYzewblL42WadJAl/uDmvbHT78EYkXiJEhHdtgHyABToiQI+LgQqv46HPPmUYLwIK5/iMvd/kBXDgI4WPxMC" +
            "HCQRXNyPx/HbWBfe0xGZ/adMWAsWgdiLmPDGFG4mRQYamDs29AY8FgooHeYhH9tvqgI4Vm2PydibVzK40wAYj" +
            "AHXcRuWgrVyYewQ0hAHMzf94TwcSGJNiRLiSzVWyKQg2CPGaEzD2N5/jLITX12a44CSGWXSA2BAzxZP1VmA/v" +
            "EvrGP7GvVFhjJvpMq+18eRGSr0ljwt7QtElV9v+9FrCu3jgoKuoxdxGOmZrXaSgAM+QlNrXI5sDNNehSs0xoV" +
            "HWBRROrji8SKCB0gMhjg+3FB1Jy0GWfomzFs0nYsjOu98izWYkZSXsZnMxritoFtn7QN8yN8IZ/YN/ZRa3tpL" +
            "JJoLbNyE5QNyIgvReUO+QE/25JhMwH4P5rehEL17dGILB0nYG2mTEgYuanFwqbdWiggGab8JdBg4Rnc2fkaD7" +
            "7rhDq0L1q1GPFetB3xsGE2IFFMtQ07oda2Fwhm2/OXBN+aYopJo8O2lmOSoNccbBC1kWOXY9iWHLtGT+0Fq+" +
            "9VtyXUSp9x+kmcs7Jf7a44f9LUCrTF5+OHVL+Owp/0TRradhSaR9EaLOAZpqjN+Fr6RSluNxrN2MbWo7Asw" +
            "XsQPjmOkqtwqdmYbAjBWdJpNzaJxmad1gd2Z4/sKlkwCivMZry2qUlBJ51cZZx1GEsUkCojsSBcGaGmVL71O" +
            "2HKzlA8yvs50tPhRHzYEN0hwoFt2UU2fdsR/FCOb3QCJ2yRDVppmb4f80QverEtFLOv5o/AStgpdhOA+af0BL+" +
            "NAKIlZ0HUHbwUA0TlMH/oKwgfmkFloWPe5FMP4kaFxiihUoRmbJWaoCTbYvJFvF0V1mosBsxHj9yF0QgaFIpZ" +
            "u9/+rEZRAswLxH/CQiH0H02sjFvp2ujbajDaQ7AEXcFxxHPJkBPC7/6EYHEFG2im5ZigG2gxGC3LfYehonKYn" +
            "2OVY5bX7TVFCcBC+jdCOV5q0Rr3wpjBi9FbRNaulVgqwi95tUNgt+BYdBiLTt/3wh9aWTSgCcoqlaU4Abb+iY" +
            "P3gyD0sMy7RpWuh0HT92Heu/scWV83D6sdoZe0+iGwfqH6sfjtL0yt2w6jtLKOYvTwlsU4pFSe4gRgga2/Aj/" +
            "8YeF1LVruL9Fo4yOcJ1lvZzJoXVG9eR63iVAzW0iWrOoQMF//G3dUsPqsu4dTX5ZHMxW8cbFYKEa/jqvT2n6H" +
            "EiAoCC9nD4SikwtryqztS5wcOWjq7gWdUBaz1m9r380LOGCr3wP7XC7h6YvI4THg9xrxmCcPQF5oWDg2OUYdE" +
            "elQArDgqf0w54GGh0Isk5qp71zsWnAGm2ld8a/B5J4QPpmGexXh2xa0dndKdzgyBj5zjmHNyFURhyiaTtcGB+" +
            "EwxyYcLA4nAMuvlh5fwoJQXteyZc6NMvTN8GrAAvQjuQZqNuu2ZThg0cEOm0pl51sEGPutK3Hw7Y64Vwxtx6A" +
            "zjW1o9nwQerTRmgqLGaHV01NMWuu0s09VApDMsObFoOiGMaJ1tBrKNUR/Aius/3wMImab3xQrJsKrXxZL+MRb" +
            "XT4JXe0oN3Wbgu40lqH122Mk0RqoaV5blkAuEqrqoqTaBMDpvbg8sTsakGK9qvGb79CbwVt2FftL1MLnpCgWg" +
            "KPaYhx9E4VRn6xORIAxH90G462pLFgWn/znPLbS2IVVb45hRONDTZfKMXhqN66oFa46AVjhQrpAtmcVAnldz/" +
            "ZBJhQetgwHCdDwNXdwotkANMucGWlIJyeEZcEwNKN1WZ2IQCTmESfl/NRY0z5oyF9UPEYTNuFkhhwopbc5HHsc" +
            "gyJ6hBKAFXepge6nd2MRrzuhGVKlxac/9sXUBVdwl8A27w7Hixq/IM4nfpxgk7gKN5RQ+Sd02vUGT3gM+Kmxp" +
            "gOxgL+oyBXhmCIZdivHHMeeXUI7BJoY27w4Gl4/i1V2dOnSbTTiPZpKza6LcCk0TgRSpEVekw+SEAH/CtPCef" +
            "XaWazmmNNCo1ELISyjQR7UuHAUS3hdNomAXghcPIY/G+ZBda3ka5YAbNAPBfHtgQ3Q5fE4yOL1CBxYj3HfF0A" +
            "9LYHQNAHYsA4V0GHOYGjxEA2Lk00iEIHArEFo2qES2kPjonkCsH1T+mBWuwrgy9yq5mZZhmzegYACL8M5lqb1" +
            "xUwFtA6T6JIAbMWhDThTxIBET0PAN7vxLtkkAg4h8CQEFziGOJYcYnSAWLcEiLQhtHwqfLxiCrpEbsuFREARB" +
            "qsmo1uFVMgJQPNbbkjm26p3AkQoGtkSo+pnRMqnj6H6il2EIPnh8QhQjFytlx6pAlthhDOcdUoCsCM3buBR+R" +
            "TI+ls71KDL6G8gi0TgHQQ4JsZ2QE2KkSxBQQh5p0vXVaclQJQXS8djVVEjEqz4A/zuUc1epRElXy7dDgHz8sn" +
            "ozTGxZCxWwsnF6QkQ6Z9l5C8YSic4viunoDsskL8IkcB4zcKCsFVT0YNjYFQrDCG/LdScXuMrAaIcNY9oicAiR" +
            "vgN/hkVnz3GjagOufRMBF49x80Rv6ASjblvYAsMJy/j9SggvhOA/P9/XTcDf32TAhmrByDJ6pno/+Y1/vl/j/x" +
            "0dwR4LFdNwcBqqZG0dGJkWPkH1ruKTy6TAFGABAfj2fCmGFAiIZLX+QjvT+6Oxvdu4ggdJsl7/uEmxYKw+0E4" +
            "MrUnmvAY8lgGtkS/Bw/w1NU8cLkEeBeg27fxYm4g5tfMgEL8k1kkPRL1rI2v1s7GoGtnsC/0FR7S7AEnRrwcP" +
            "75rqxeuWxj71zQGPBbrZ2Noj1oowmPEY1UjPQrNHoZ5PIaujI1LJ0As4ILwcvty7B/6I/o2zIsiJd+DP80e+NK" +
            "JlJGaD7UENIeciA+jqqRCskqZkaJaJnzYrhTyT+qC73atxtRH93COBk7Xiyux7I65w/W2LWGheMrYnDmIFZsX" +
            "Y+CEzmjYtgwKNy2ATHXTwZ8xZWwZY2qMtZGxL0VjwGMx6Ef02rEC+0Bj5HruxW2ReyVA3H5wD993FEpzyC/5M" +
            "CokBE8eX8PjB9dx59B2HF8wCgu7VUeLymmRiwYuAQ2iX7tvkH/HKox/8Qx3WYBXNDo8Cb6JE4vHoGvrEshNOC" +
            "SkZvw6AZIyNs0Ko1bf+ui3aDQWHd6Kg38fw/Vbt/CQMWVsAfCXB2NNq+5fPSkBHB2NN4c24XiPGmhXNgk+oCA" +
            "wdSiPQmcPg+ei+bAKnlJePEXQolFoUzkrkvPhSfUMyDeuI0Ye3Ym/yUfhN3+TDLet3pwAMQfNfGAjjvz8OWpS" +
            "Mvi2LIEsp/ZjGRHF6zQd6VdVzeF4uex38Dx7wrJJkX5CF0x8dEXOrCFGkQkQA5CozZM7cbXFV6hDyeAzoQNqh" +
            "obiYVSfKy+fP8HtPvVQpJgPEo3+FTzP7tXf8PbGSiaAPYQAyyK6RF8yAfw7VMRndDjB7yS1z+VkitDXuM8n++" +
            "WS4aMtf2Kfk9UrVudqhEZXM8iV7TnwF07S4cRHQ5uiPM0kucbtGxaEj+uA2iUTIg2f7Lsyfq5om0wAFaOydi" +
            "Y28kzSheNYq4JdM5Zrp7GOTmoTLB6L5ZoJ9TJBMgHUD7jlh/yoOrkXGqsXoZrTEtgcFRp+gsokwWOmJMkXp1" +
            "eZAIKQzx2K+T1rgf83QVCSMnaLGS/r5kLqVdMg/DeiyjR6NpVMAA3Gd/sK7P6jO6ppIMqmCAr+Z0VzIsWtc3C" +
            "LGSmbzrhIpxMTwEU81smMeYFYs3ctJukknsWaa6TFB7gEOa3JaGjUZAJoBCSL6VwVrcPC9LnYRHP7xfh2BNYj" +
            "m3YIyATQDssISXStQPPzgTvXsFfO7UfAq/mHTACNIT2yGafoahvx/bwAAAQWSURBVOxVLcV2KhUx26OlSCkrEg" +
            "GZAJFAaLkY+hMaaCXvwW2cunYNjyGLLgjIBNABVv7vMtAV2reiBVYCW0G+Z1UAP3usMgHsIaSy/+ppCM/T8+0W" +
            "e1bjsEoTJJsCBGQCKABJDcm80fhNDd+7PAc2YNa723JdewRkAmiPaYTEk5vE78ic3B0DI4TJD90QkAmgE7TCD4" +
            "PTOcSFE7ilk3lSbCQCMgEigdBlYYl4flaV6JD7uKiK0cWYXN0cmQA6jtCbN3ipVvzZA9imllfyKUdAJoByrNRQ" +
            "WtQwMc+JndjKS9n0RUAmgI74GgxIqFb8mT1y+lMtdo7wyQRwBC0HaX18kcBBlrfkx/fLE+C3YOi4IhNAP3D9S" +
            "LSBmsOVLoDxe4lc45ljh613LwYdE8C9gNDa2lK18LlamcG3cU4tr+RzDAGZAI7hpZi6SW/0VEwcg3DHcsyGLE5" +
            "BQCaAPjAbcuZHBbWil07CIrW8ks8xBGQCOIaXIur67VGbCNVia755Hi758i3yyeOq2kHyOCC0dOjXkVB9CHP7K" +
            "o5CFqchIBNAY6jbjkEbkw/eVyt24XD0hyxOQ0AmgIZQp06NxHT4M15E5PKp+EuEX/I6hoBMAMfwskm94CJO2yS" +
            "w0/nwbgS/fNObHZy07JYJoBGaEzZhauJkyAiBMr4dWgqwS1YVCMgEUAFaTJa2o/BzwbJoFnO/I9t09Td00xLsc" +
            "YRH0oojIBNAEMNqzVCtfidMExSDFZPRTVSGK/C7mw0yAQRGrF471O4+FasERESxho/6BeOiNuTSeQjIBFCJdZu" +
            "RaN1+LJaqZI/GNmcwWtAO1c8OEK+sKhGQCaACuOErML5BZ0xQwRqL5U0oHk/pgxmxOuQOpyAgE8BBmGcfxbqv" +
            "a6CNg2xxkncsj6/i7JQduiMgE8ABiNfcwd4c+VHRARabpOf3Y8GR7ZC3PttESd9ODRNAX0PjW/rCv7EtVVpo9m" +
            "0dHoYnP36FRvHtl7frlwmgIAJK10eRTB+jJDQsP+ZCNg3FSVEqEZAJoAC4aj9GzNIooFRGMqgJyly6hGBl1JJ" +
            "KTwRkAihA12BEgAIyRSRzh6H5+nnYqohYEumOgEwABRCHm3FTAZldkrlD0XFyTwhfNbarSBIoRkAmgAKo9v2F" +
            "9TbJFHTOGY5Wk3thjAJSSeJEBGQCKAD7zz8hdI/+0B9ReUoPTFagSpI4GQGZAEoAD8LLO9dwTAlpDJqw1kWQfe" +
            "1srIuxX266CAIyARQOxC9fojiRmqkpqk8f43IRAxIf3YdLihgkUbwgIBNAIez37uF5wzz4yByO53ZYLIvGoE35F" +
            "BHz/PJPre2AFd/dMgEcGIFrZ3G3mA8ST++NRk9CwH+FGvX4ovn1c9yaPwJt6FvfZ0JHTHRArFuTurvx/wMAAP" +
            "//Jk2bgAAAAAZJREFUAwCwqKkC7bBPoAAAAABJRU5ErkJggg==";

        #endregion
    }
}
