// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Diagnostics;

namespace Cotton.Server.Handlers.Computation
{
    public record GetTextEmbeddingFragmentsRequest(IAsyncEnumerable<string> Texts) : IRequest<float[][][]>;

    public class GetTextEmbeddingFragmentsRequestHandler(
        IMediator mediator, SettingsProvider settings, TeiClient client, TextEmbeddingChunker chunker,
        ILogger<GetTextEmbeddingFragmentsRequestHandler> logger)
        : IRequestHandler<GetTextEmbeddingFragmentsRequest, float[][][]>
    {
        private static readonly TimeSpan ProgressLogInterval = TimeSpan.FromSeconds(10);

        public async Task<float[][][]> Handle(GetTextEmbeddingFragmentsRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request.Texts);
            long startedAt = Stopwatch.GetTimestamp();
            long lastProgressAt = startedAt;
            int computedVectors = 0;
            Uri? url = null;
            ComputationServiceInfo? info = null;
            List<string> batch = [];
            List<int> owners = [];
            List<List<float[]>> result = [];
            int longestInput = 0;
            await foreach (string text in request.Texts.WithCancellation(cancellationToken))
            {
                ArgumentNullException.ThrowIfNull(text);
                int owner = result.Count;
                result.Add([]);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }
                if (info is null)
                {
                    string configuredUrl = GetComputationServiceInfoQueryHandler.ResolveConfiguredUrl(settings.GetServerSettings());
                    url = TextEmbeddingValidation.NormalizeUrl(configuredUrl);
                    info = await mediator.Send(new GetComputationServiceInfoQuery(configuredUrl), cancellationToken);
                }
                int maxTokens = Math.Min(info.MaxInputTokens, info.MaxBatchTokens);
                await foreach (TextEmbeddingChunk chunk in chunker.SplitAsync(url!, text, maxTokens, cancellationToken))
                {
                    int nextLongestInput = Math.Max(longestInput, chunk.TokenCount);
                    if (batch.Count > 0 && (batch.Count == info.MaxBatchInputs
                        || (long)nextLongestInput * (batch.Count + 1) > info.MaxBatchTokens))
                    {
                        await FlushAsync();
                        longestInput = 0;
                    }
                    batch.Add(chunk.Text);
                    owners.Add(owner);
                    longestInput = Math.Max(longestInput, chunk.TokenCount);
                }
            }
            if (batch.Count > 0)
            {
                await FlushAsync();
            }
            return result.Select(vectors => vectors.ToArray()).ToArray();

            async Task FlushAsync()
            {
                float[][] vectors = await client.GetTextEmbeddingsAsync(url!, [.. batch], cancellationToken);
                TextEmbeddingValidation.ValidateVectors(vectors, batch.Count);
                for (int index = 0; index < vectors.Length; index++)
                {
                    result[owners[index]].Add(vectors[index]);
                }
                computedVectors += vectors.Length;
                if (Stopwatch.GetElapsedTime(lastProgressAt) >= ProgressLogInterval)
                {
                    logger.LogInformation("Computed {VectorCount} text embedding vectors in {ElapsedSeconds:F1} seconds.",
                        computedVectors, Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
                    lastProgressAt = Stopwatch.GetTimestamp();
                }
                batch.Clear();
                owners.Clear();
            }
        }
    }
}
