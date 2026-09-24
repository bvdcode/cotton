// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;

namespace Cotton.Server.Models.Configuration
{
    public class TextIndexingOptions
    {
        public const string SectionName = "TextIndexing";

        public static readonly string[] StructuredContentTypes =
        [
            "text/csv",
            "text/tab-separated-values",
            "application/json",
            "text/xml",
            "application/xml",
            "text/yaml",
            "application/yaml",
            "application/x-yaml",
            "text/css",
            "text/javascript",
            "application/javascript",
            "text/typescript",
            "application/typescript",
            "application/x-ipynb+json",
        ];

        public int MaxExtractedTextBytes { get; set; } = FileTextExtractor.DefaultMaxUtf8Bytes;

        public int MaxHtmlExtractedTextBytes { get; set; } = 64 * 1024 * 1024;

        public long MaxStructuredFileBytes { get; set; } = 1024 * 1024;
    }
}
