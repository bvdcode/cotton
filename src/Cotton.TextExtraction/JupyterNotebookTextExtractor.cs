// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cotton.TextExtraction
{
    public class JupyterNotebookTextExtractor(ILogger<JupyterNotebookTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "application/x-ipynb+json";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            try
            {
                using JsonDocument document = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("cells", out JsonElement cells)
                    || cells.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("The notebook has no cells array.");
                }

                foreach (JsonElement cell in cells.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendCellSource(cell, text);
                    text.AppendLineBreak();
                    if (text.IsTruncated)
                    {
                        return;
                    }
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Unable to extract Jupyter notebook text.");
                throw new FileTextExtractionException("Unable to read Jupyter notebook text.", ex);
            }
        }

        private static void AppendCellSource(JsonElement cell, TextExtractionBuffer text)
        {
            if (cell.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A notebook cell is invalid.");
            }
            if (!cell.TryGetProperty("source", out JsonElement source))
            {
                return;
            }

            switch (source.ValueKind)
            {
                case JsonValueKind.String:
                    text.AppendNormalized(source.GetString());
                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement line in source.EnumerateArray())
                    {
                        text.AppendNormalized(ReadSourceLine(line));
                        if (text.IsTruncated)
                        {
                            return;
                        }
                    }
                    break;
                default:
                    throw new JsonException("A notebook cell has an invalid source.");
            }
        }

        private static string ReadSourceLine(JsonElement line)
        {
            return line.ValueKind switch
            {
                JsonValueKind.String => line.GetString() ?? string.Empty,
                _ => throw new JsonException("A notebook cell source line is invalid."),
            };
        }
    }
}
