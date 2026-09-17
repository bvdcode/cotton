// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Text.Json;

namespace Cotton.Server.Handlers.Computation
{
    public record GetComputationStatusQuery(string? Url = null, bool ForceRefresh = false)
        : IRequest<ComputationStatus>;

    public class GetComputationStatusQueryHandler(
        IMediator mediator, SettingsProvider settings, TeiClient client,
        EmbeddingDimensionCache cache, ILogger<GetComputationStatusQueryHandler> logger)
        : IRequestHandler<GetComputationStatusQuery, ComputationStatus>
    {
        private const string ProbeText = "Cotton search connection test.";

        public async Task<ComputationStatus> Handle(
            GetComputationStatusQuery request, CancellationToken cancellationToken)
        {
            ComputationServiceInfo? info = null;
            try
            {
                string url = request.Url
                    ?? GetComputationServiceInfoQueryHandler.ResolveConfiguredUrl(settings.GetServerSettings());
                Uri baseUri = TextEmbeddingValidation.NormalizeUrl(url);
                info = await mediator.Send(new GetComputationServiceInfoQuery(url), cancellationToken);
                int dimensions = await cache.GetOrProbeAsync(
                    baseUri, info, request.ForceRefresh,
                    async () =>
                    {
                        float[][] vectors = await client.GetTextEmbeddingsAsync(baseUri, [ProbeText], cancellationToken);
                        TextEmbeddingValidation.ValidateVectors(vectors, 1);
                        return vectors[0].Length;
                    },
                    cancellationToken);
                return new ComputationStatus(info, dimensions, null);
            }
            catch (ComputationException ex)
            {
                logger.LogWarning(ex, "Text embedding service validation failed: {Code}", ex.Error);
                return new ComputationStatus(info, null, ex.Error);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Text embedding service is unavailable.");
                return new ComputationStatus(info, null, ComputationError.Unreachable);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Text embedding service request timed out.");
                return new ComputationStatus(info, null, ComputationError.Timeout);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Text embedding service returned an invalid response.");
                return new ComputationStatus(info, null, ComputationError.InvalidResponse);
            }
        }
    }
}
