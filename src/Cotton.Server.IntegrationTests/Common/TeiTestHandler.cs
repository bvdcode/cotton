// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Cotton.Server.Models.Computation;
using Cotton.Server.Services.Bridge;

namespace Cotton.Server.IntegrationTests.Common
{
    public class TeiTestHandler : HttpMessageHandler
    {
        public string ModelId { get; set; } = "BAAI/bge-m3";
        public string? ModelRevision { get; set; }
        public bool IsEmbeddingModel { get; set; } = true;
        public int Dimensions { get; set; } = 1024;
        public int MaxBatchInputs { get; set; } = 32;
        public int MaxInputTokens { get; set; } = 8192;
        public int MaxBatchTokens { get; set; } = 16384;
        public int CountOffset { get; set; }
        public bool ZeroVectors { get; set; }
        public bool InvalidJson { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public int InfoCalls { get; private set; }
        public int EmbedCalls { get; private set; }
        public int? FailingEmbeddingCall { get; set; }
        public Action? EmbeddingRequested { get; set; }
        public int TokenizeCalls { get; private set; }
        public List<int> TokenizationInputLengths { get; } = [];
        public List<string[]> Batches { get; } = [];
        public List<Uri> Addresses { get; } = [];
        public List<(string? Token, string? InstanceId)> Credentials { get; } = [];
        public bool? Truncate { get; private set; }
        public bool? Normalize { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Addresses.Add(request.RequestUri!);
            Credentials.Add((request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues(BridgeCredentialProvider.InstanceIdHeader, out IEnumerable<string>? ids)
                    ? string.Join(",", ids) : null));
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
                        max_input_length = MaxInputTokens, max_batch_tokens = MaxBatchTokens,
                        max_client_batch_size = MaxBatchInputs, max_concurrent_requests = 512
                    })
                };
            }

            JsonElement body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (request.RequestUri.AbsolutePath.EndsWith("/tokenize", StringComparison.Ordinal))
            {
                TokenizeCalls++;
                string input = body.GetProperty("inputs").GetString()!;
                TokenizationInputLengths.Add(input.Length);
                List<TeiToken> tokens = [new(true, null, null)];
                int position = 0;
                foreach (Rune rune in input.EnumerateRunes())
                {
                    tokens.Add(new(false, position, position + rune.Utf8SequenceLength));
                    position += rune.Utf8SequenceLength;
                }
                tokens.Add(new(true, null, null));
                return new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(new[] { tokens }) };
            }
            EmbedCalls++;
            EmbeddingRequested?.Invoke();
            if (EmbedCalls == FailingEmbeddingCall)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
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
