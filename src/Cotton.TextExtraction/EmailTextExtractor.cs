// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using MimeKit;

namespace Cotton.TextExtraction
{
    public class EmailTextExtractor(ILogger<EmailTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "message/rfc822";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            try
            {
                using MimeMessage message = await MimeMessage.LoadAsync(source, cancellationToken);
                text.AppendLines([message.Subject, message.From.ToString(), message.To.ToString()], cancellationToken);
                if (text.IsTruncated)
                {
                    return;
                }
                string? body = message.TextBody;
                if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    await HtmlTextExtractor.ExtractIntoAsync(message.HtmlBody, text, cancellationToken);
                }
                else
                {
                    text.AppendNormalized(body);
                }
            }
            catch (ParseException ex)
            {
                logger.LogWarning(ex, "Unable to extract email text.");
                throw new FileTextExtractionException("Unable to read email text.", ex);
            }
        }
    }
}
