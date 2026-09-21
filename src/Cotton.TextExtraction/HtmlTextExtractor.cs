// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Cotton.TextExtraction
{
    public class HtmlTextExtractor : FileTextExtractor
    {
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

        protected override Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            return ExtractIntoAsync(source, text, cancellationToken);
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
