// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;

namespace Cotton.Server.Models.Configuration
{
    public class TextIndexingOptions
    {
        public const string SectionName = "TextIndexing";

        public int MaxExtractedTextBytes { get; set; } = FileTextExtractor.DefaultMaxUtf8Bytes;
    }
}
