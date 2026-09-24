// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System.Text;
using System.Text.Json;

namespace Cotton.TextExtraction
{
    public class HtmlTextExtractor : FileTextExtractor
    {
        private const int ProbeSize = 64 * 1024;
        private const int MaxHtmlSourceBytes = 16 * 1024 * 1024;

        private static readonly byte[] ChatExportTitle = Encoding.UTF8.GetBytes("<title>ChatGPT Data Export</title>");
        private static readonly byte[] ChatExportMarker = Encoding.UTF8.GetBytes("var jsonData = ");

        private static readonly IReadOnlySet<string> IgnoredElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "template", "noscript",
        };

        private static readonly IReadOnlySet<string> BlockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "address", "article", "aside", "blockquote", "br", "dd", "div", "dl", "dt", "fieldset",
            "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header",
            "hr", "li", "main", "nav", "ol", "p", "pre", "section", "table", "td", "th", "tr", "ul",
        };

        public static readonly string[] ContentTypes = ["text/html", "application/xhtml+xml"];

        public override IEnumerable<string> SupportedContentTypes => ContentTypes;

        protected override async Task ExtractAsync(
            Stream source,
            TextExtractionBuffer text,
            CancellationToken cancellationToken)
        {
            ReadOnlyMemory<byte> prefix = await ReadPrefixAsync(source, cancellationToken);
            int jsonOffset = FindChatExportJson(prefix.Span);
            if (jsonOffset >= 0)
            {
                try
                {
                    using PrefixedReadStream replay = new(prefix[jsonOffset..], source);
                    using JsonArrayReadStream json = new(replay);
                    await ChatExportTextExtractor.ExtractAsync(json, text, cancellationToken);
                    return;
                }
                catch (Exception exception) when (exception is JsonException or InvalidDataException)
                {
                    throw new FileTextExtractionException("Unable to read the chat export.", exception);
                }
            }

            using PrefixedReadStream limited = new(prefix, source, MaxHtmlSourceBytes);
            await ExtractIntoAsync(limited, text, cancellationToken);
            if (limited.IsTruncated)
            {
                text.MarkTruncated();
            }
        }

        internal static async Task ExtractIntoAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            HtmlParser parser = new();
            using IHtmlDocument document = await parser.ParseDocumentAsync(source, cancellationToken);
            AppendNode(document.Body ?? document.DocumentElement, text, cancellationToken);
        }

        internal static async Task ExtractIntoAsync(string html, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            HtmlParser parser = new();
            using IHtmlDocument document = await parser.ParseDocumentAsync(html, cancellationToken);
            AppendNode(document.Body ?? document.DocumentElement, text, cancellationToken);
        }

        private static async Task<ReadOnlyMemory<byte>> ReadPrefixAsync(
            Stream source,
            CancellationToken cancellationToken)
        {
            byte[] prefix = new byte[ProbeSize];
            int length = 0;
            while (length < prefix.Length)
            {
                int read = await source.ReadAsync(prefix.AsMemory(length), cancellationToken);
                if (read == 0)
                {
                    break;
                }
                length += read;
            }
            return prefix.AsMemory(0, length);
        }

        private static int FindChatExportJson(ReadOnlySpan<byte> source)
        {
            if (source.IndexOf(ChatExportTitle) < 0)
            {
                return -1;
            }
            int marker = source.IndexOf(ChatExportMarker);
            if (marker < 0)
            {
                return -1;
            }
            int offset = marker + ChatExportMarker.Length;
            while (offset < source.Length && char.IsWhiteSpace((char)source[offset]))
            {
                offset++;
            }
            return offset < source.Length && source[offset] == (byte)'[' ? offset : -1;
        }

        private static void AppendNode(INode? node, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is null || text.IsTruncated)
            {
                return;
            }
            if (node is IText characterData)
            {
                text.AppendNormalized(characterData.Data);
                return;
            }
            if (node is IElement element && IgnoredElements.Contains(element.LocalName))
            {
                return;
            }

            bool isBlock = node is IElement block && BlockElements.Contains(block.LocalName);
            if (isBlock)
            {
                text.AppendLineBreak();
            }
            foreach (INode child in node.ChildNodes)
            {
                AppendNode(child, text, cancellationToken);
                if (text.IsTruncated)
                {
                    break;
                }
            }
            if (isBlock)
            {
                text.AppendLineBreak();
            }
        }
    }
}
