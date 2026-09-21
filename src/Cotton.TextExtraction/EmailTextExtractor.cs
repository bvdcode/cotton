// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using MimeKit;

namespace Cotton.TextExtraction
{
    public class EmailTextExtractor(ILogger<EmailTextExtractor> logger) : IFileTextExtractor
    {
        public const string ContentType = "message/rfc822";

        public IEnumerable<string> SupportedContentTypes => [ContentType];

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                MimeMessage message = await MimeMessage.LoadAsync(source, cancellationToken);
                string? body = message.TextBody;
                if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    body = await HtmlTextExtractor.ExtractAsync(message.HtmlBody, cancellationToken);
                }
                return TextExtractionUtilities.JoinLines(
                [
                    message.Subject,
                    message.From.ToString(),
                    message.To.ToString(),
                    body,
                ]);
            }
            catch (ParseException ex)
            {
                logger.LogWarning(ex, "Unable to extract email text.");
                throw new FileTextExtractionException("Unable to read email text.", ex);
            }
        }
    }
}
