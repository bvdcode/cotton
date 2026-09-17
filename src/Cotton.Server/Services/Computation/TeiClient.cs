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
