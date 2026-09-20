// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Computation
{
    public record GetTextEmbeddingFragmentsRequest(string Text) : IRequest<float[][]>;

    public class GetTextEmbeddingFragmentsRequestHandler(
        IMediator mediator, SettingsProvider settings, TeiClient client, TextEmbeddingChunker chunker)
        : IRequestHandler<GetTextEmbeddingFragmentsRequest, float[][]>
    {
        public async Task<float[][]> Handle(GetTextEmbeddingFragmentsRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request.Text);
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return [];
            }
            string configuredUrl = GetComputationServiceInfoQueryHandler.ResolveConfiguredUrl(settings.GetServerSettings());
            Uri url = TextEmbeddingValidation.NormalizeUrl(configuredUrl);
            ComputationServiceInfo info = await mediator.Send(new GetComputationServiceInfoQuery(configuredUrl), cancellationToken);
            int maxTokens = Math.Min(info.MaxInputTokens, info.MaxBatchTokens);
            List<string> batch = [];
            List<float[]> result = [];
            int longestInput = 0;
            await foreach (TextEmbeddingChunk chunk in chunker.SplitAsync(url, request.Text, maxTokens, cancellationToken))
            {
                int nextLongestInput = Math.Max(longestInput, chunk.TokenCount);
                if (batch.Count > 0 && (batch.Count == info.MaxBatchInputs
                    || (long)nextLongestInput * (batch.Count + 1) > info.MaxBatchTokens))
                {
                    await FlushAsync();
                    longestInput = 0;
                }
                batch.Add(chunk.Text);
                longestInput = Math.Max(longestInput, chunk.TokenCount);
            }
            if (batch.Count > 0)
            {
                await FlushAsync();
            }
            return result.ToArray();

            async Task FlushAsync()
            {
                float[][] vectors = await client.GetTextEmbeddingsAsync(url, [.. batch], cancellationToken);
                TextEmbeddingValidation.ValidateVectors(vectors, batch.Count);
                result.AddRange(vectors);
                batch.Clear();
            }
        }
    }
}
