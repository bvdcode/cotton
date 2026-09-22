// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text.Json;

namespace Cotton.TextExtraction
{
    internal static class ChatExportTextExtractor
    {
        public static async Task ExtractAsync(
            Stream source,
            TextExtractionBuffer text,
            CancellationToken cancellationToken)
        {
            await foreach (JsonElement conversation in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
                source, cancellationToken: cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (conversation.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                AppendConversation(conversation, text, cancellationToken);
                if (text.IsTruncated)
                {
                    return;
                }
            }
        }

        private static void AppendConversation(
            JsonElement conversation,
            TextExtractionBuffer text,
            CancellationToken cancellationToken)
        {
            if (conversation.TryGetProperty("title", out JsonElement title)
                && title.ValueKind == JsonValueKind.String)
            {
                AppendDecoded(text, title.GetString());
                text.AppendLineBreak();
            }
            if (!conversation.TryGetProperty("mapping", out JsonElement mapping)
                || mapping.ValueKind != JsonValueKind.Object
                || !conversation.TryGetProperty("current_node", out JsonElement currentNodeElement)
                || currentNodeElement.ValueKind != JsonValueKind.String)
            {
                return;
            }
            Dictionary<string, JsonElement> nodes = mapping.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            List<JsonElement> path = [];
            string? currentNode = currentNodeElement.GetString();
            while (currentNode is not null && nodes.TryGetValue(currentNode, out JsonElement node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                path.Add(node);
                currentNode = node.TryGetProperty("parent", out JsonElement parent)
                    && parent.ValueKind == JsonValueKind.String
                    ? parent.GetString()
                    : null;
            }
            for (int index = path.Count - 1; index >= 0 && !text.IsTruncated; index--)
            {
                AppendMessage(path[index], text);
            }
            text.AppendLineBreak();
        }

        private static void AppendMessage(JsonElement node, TextExtractionBuffer text)
        {
            if (!node.TryGetProperty("message", out JsonElement message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("author", out JsonElement author)
                || !author.TryGetProperty("role", out JsonElement roleElement)
                || roleElement.ValueKind != JsonValueKind.String
                || !message.TryGetProperty("content", out JsonElement content)
                || content.ValueKind != JsonValueKind.Object
                || !content.TryGetProperty("content_type", out JsonElement contentType)
                || contentType.ValueKind != JsonValueKind.String
                || contentType.GetString() is not ("text" or "multimodal_text")
                || !content.TryGetProperty("parts", out JsonElement parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            string role = roleElement.GetString()!;
            bool isCustomSystemMessage = role == "system"
                && message.TryGetProperty("metadata", out JsonElement metadata)
                && metadata.TryGetProperty("is_user_system_message", out JsonElement custom)
                && custom.ValueKind is JsonValueKind.True;
            if (role == "system" && !isCustomSystemMessage)
            {
                return;
            }
            foreach (JsonElement part in parts.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String && part.GetString() is string value)
                {
                    AppendDecoded(text, value);
                }
                else if (part.ValueKind == JsonValueKind.Object
                    && part.TryGetProperty("content_type", out JsonElement partType)
                    && partType.ValueKind == JsonValueKind.String
                    && partType.GetString() == "audio_transcription"
                    && part.TryGetProperty("text", out JsonElement transcript)
                    && transcript.ValueKind == JsonValueKind.String)
                {
                    AppendDecoded(text, transcript.GetString());
                }
                text.AppendLineBreak();
                if (text.IsTruncated)
                {
                    return;
                }
            }
        }

        private static void AppendDecoded(TextExtractionBuffer text, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                text.AppendNormalized(WebUtility.HtmlDecode(value));
            }
        }
    }
}
