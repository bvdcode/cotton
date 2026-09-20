// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Computation
{
    public record GetTextEmbeddingsRequest(string[] Texts) : IRequest<float[][]>;

    public class GetTextEmbeddingsRequestHandler(IMediator mediator, SettingsProvider settings, TeiClient client)
        : IRequestHandler<GetTextEmbeddingsRequest, float[][]>
    {
        public async Task<float[][]> Handle(GetTextEmbeddingsRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request.Texts);
            if (request.Texts.Any(string.IsNullOrWhiteSpace))
            {
                throw new ComputationException(ComputationError.InvalidInput);
            }
            if (request.Texts.Length == 0)
            {
                return [];
            }

            string configuredUrl = GetComputationServiceInfoQueryHandler.ResolveConfiguredUrl(settings.GetServerSettings());
            Uri url = TextEmbeddingValidation.NormalizeUrl(configuredUrl);
            ComputationServiceInfo info = await mediator.Send(
                new GetComputationServiceInfoQuery(configuredUrl), cancellationToken);
            List<float[]> result = new(request.Texts.Length);
            foreach (string[] batch in request.Texts.Chunk(info.MaxBatchInputs))
            {
                float[][] vectors = await client.GetTextEmbeddingsAsync(url, batch, cancellationToken);
                TextEmbeddingValidation.ValidateVectors(vectors, batch.Length);
                result.AddRange(vectors);
            }
            return result.ToArray();
        }
    }
}
