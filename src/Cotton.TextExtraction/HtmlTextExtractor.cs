// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System.Text;

namespace Cotton.TextExtraction
{
    public class HtmlTextExtractor : IFileTextExtractor
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

        public IEnumerable<string> SupportedContentTypes => ContentTypes;

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            HtmlParser parser = new();
            IHtmlDocument document = await parser.ParseDocumentAsync(source, cancellationToken);
            return ExtractDocument(document);
        }

        internal static async Task<string> ExtractAsync(string html, CancellationToken cancellationToken)
        {
            HtmlParser parser = new();
            IHtmlDocument document = await parser.ParseDocumentAsync(html, cancellationToken);
            return ExtractDocument(document);
        }

        private static string ExtractDocument(IHtmlDocument document)
        {
            StringBuilder text = new();
            AppendNode(document.Body ?? document.DocumentElement, text);
            return TextExtractionUtilities.JoinLines(text.ToString().Split('\n'));
        }

        private static void AppendNode(INode? node, StringBuilder text)
        {
            if (node is null)
            {
                return;
            }
            if (node is IText characterData)
            {
                text.Append(characterData.Data);
                return;
            }
            if (node is IElement element && IgnoredElements.Contains(element.LocalName))
            {
                return;
            }

            bool isBlock = node is IElement block && BlockElements.Contains(block.LocalName);
            if (isBlock)
            {
                AppendLineBreak(text);
            }
            foreach (INode child in node.ChildNodes)
            {
                AppendNode(child, text);
            }
            if (isBlock)
            {
                AppendLineBreak(text);
            }
        }

        private static void AppendLineBreak(StringBuilder text)
        {
            if (text.Length > 0 && text[^1] != '\n')
            {
                text.Append('\n');
            }
        }
    }
}
