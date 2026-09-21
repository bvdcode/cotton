// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cotton.TextExtraction
{
    public class JupyterNotebookTextExtractor(ILogger<JupyterNotebookTextExtractor> logger) : IFileTextExtractor
    {
        public const string ContentType = "application/x-ipynb+json";

        public IEnumerable<string> SupportedContentTypes => [ContentType];

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using JsonDocument document = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("cells", out JsonElement cells)
                    || cells.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("The notebook has no cells array.");
                }

                return TextExtractionUtilities.JoinLines(cells.EnumerateArray().Select(ReadCellSource));
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Unable to extract Jupyter notebook text.");
                throw new FileTextExtractionException("Unable to read Jupyter notebook text.", ex);
            }
        }

        private static string ReadCellSource(JsonElement cell)
        {
            if (cell.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A notebook cell is invalid.");
            }
            if (!cell.TryGetProperty("source", out JsonElement source))
            {
                return string.Empty;
            }

            return source.ValueKind switch
            {
                JsonValueKind.String => source.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Concat(source.EnumerateArray()
                    .Select(ReadSourceLine)),
                _ => throw new JsonException("A notebook cell has an invalid source."),
            };
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
