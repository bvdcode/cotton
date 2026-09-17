// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests.Common
{
    public class TeiTestHandler : HttpMessageHandler
    {
        public string ModelId { get; set; } = "BAAI/bge-m3";
        public string? ModelRevision { get; set; }
        public bool IsEmbeddingModel { get; set; } = true;
        public int Dimensions { get; set; } = 1024;
        public int MaxBatchInputs { get; set; } = 32;
        public int CountOffset { get; set; }
        public bool ZeroVectors { get; set; }
        public bool InvalidJson { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public int InfoCalls { get; private set; }
        public int EmbedCalls { get; private set; }
        public List<string[]> Batches { get; } = [];
        public List<Uri> Addresses { get; } = [];
        public bool? Truncate { get; private set; }
        public bool? Normalize { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Addresses.Add(request.RequestUri!);
            if (InvalidJson)
            {
                return new HttpResponseMessage(StatusCode) { Content = new StringContent("invalid json") };
            }
            if (request.RequestUri!.AbsolutePath.EndsWith("/info", StringComparison.Ordinal))
            {
                InfoCalls++;
                Dictionary<string, object> modelType = [];
                if (IsEmbeddingModel)
                {
                    modelType["embedding"] = new { pooling = "cls" };
                }
                return new HttpResponseMessage(StatusCode)
                {
                    Content = JsonContent.Create(new
                    {
                        model_id = ModelId, model_sha = ModelRevision, model_type = modelType,
                        max_input_length = 8192, max_batch_tokens = 16384,
                        max_client_batch_size = MaxBatchInputs, max_concurrent_requests = 512
                    })
                };
            }

            EmbedCalls++;
            JsonElement body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            string[] texts = body.GetProperty("inputs").EnumerateArray().Select(x => x.GetString()!).ToArray();
            Batches.Add(texts);
            Truncate = body.GetProperty("truncate").GetBoolean();
            Normalize = body.GetProperty("normalize").GetBoolean();
            float[][] vectors = Enumerable.Range(0, Math.Max(0, texts.Length + CountOffset))
                .Select(index =>
                {
                    float[] vector = new float[Dimensions];
                    vector[0] = ZeroVectors ? 0 : texts[index % texts.Length].Length;
                    return vector;
                }).ToArray();
            return new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(vectors) };
        }
    }
}
