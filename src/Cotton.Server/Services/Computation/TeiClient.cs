// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using System.Text.Json;

namespace Cotton.Server.Services.Computation
{
    public class TeiClient(HttpClient httpClient)
    {
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        public async Task<ComputationServiceInfo> GetServiceInfoAsync(Uri baseUri, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.GetAsync(new Uri(baseUri, "info"), cancellationToken);
            response.EnsureSuccessStatusCode();
            JsonElement root = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ComputationException(ComputationError.InvalidResponse);
            }

            bool isEmbedding = root.TryGetProperty("model_type", out JsonElement modelType)
                && modelType.ValueKind == JsonValueKind.Object
                && modelType.TryGetProperty("embedding", out JsonElement embedding)
                && embedding.ValueKind == JsonValueKind.Object;
            string? pooling = null;
            if (isEmbedding)
            {
                pooling = ReadString(modelType.GetProperty("embedding"), "pooling");
            }

            return new ComputationServiceInfo(
                ReadString(root, "model_id") ?? string.Empty,
                ReadString(root, "model_sha"),
                isEmbedding,
                pooling,
                ReadInt(root, "max_input_length"),
                ReadInt(root, "max_batch_tokens"),
                ReadInt(root, "max_client_batch_size"),
                ReadInt(root, "max_concurrent_requests"));
        }

        public async Task<TeiToken[]> TokenizeAsync(Uri baseUri, string text, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                new Uri(baseUri, "tokenize"), new { inputs = text, add_special_tokens = true }, cancellationToken);
            response.EnsureSuccessStatusCode();
            TeiToken[][]? tokens = await response.Content.ReadFromJsonAsync<TeiToken[][]>(cancellationToken);
            if (tokens is null || tokens.Length != 1 || tokens[0] is null || tokens[0].Length == 0
                || tokens[0].Any(token => token is null))
            {
                throw new ComputationException(ComputationError.InvalidResponse);
            }
            return tokens[0];
        }

        public async Task<float[][]> GetTextEmbeddingsAsync(
            Uri baseUri, string[] texts, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                new Uri(baseUri, "embed"),
                new { inputs = texts, truncate = false, normalize = true },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<float[][]>(cancellationToken)
                ?? throw new ComputationException(ComputationError.InvalidResponse);
        }

        private static string? ReadString(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int ReadInt(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
                ? number
                : 0;
        }
    }
}
