// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Computation;
using Cotton.Server.Models.Computation;
using EasyExtensions.Mediator;

namespace Cotton.Server.Services.Computation
{
    public class ComputationService(IMediator mediator)
    {
        public Task<ComputationStatus> GetStatusAsync(
            string? url = null, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            return mediator.Send(new GetComputationStatusQuery(url, forceRefresh), cancellationToken);
        }

        public Task<ComputationServiceInfo> GetServiceInfoAsync(CancellationToken cancellationToken = default)
        {
            return mediator.Send(new GetComputationServiceInfoQuery(), cancellationToken);
        }

        public Task<float[][]> GetTextEmbeddingsAsync(string[] texts, CancellationToken cancellationToken = default)
        {
            return mediator.Send(new GetTextEmbeddingsRequest(texts), cancellationToken);
        }

        public Task<float[][]> GetTextEmbeddingFragmentsAsync(string text, CancellationToken cancellationToken = default)
        {
            return mediator.Send(new GetTextEmbeddingFragmentsRequest(text), cancellationToken);
        }
    }
}
