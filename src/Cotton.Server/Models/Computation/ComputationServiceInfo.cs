// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Computation
{
    public record ComputationServiceInfo(
        string ModelId,
        string? ModelRevision,
        bool IsEmbeddingModel,
        string? Pooling,
        int MaxInputTokens,
        int MaxBatchTokens,
        int MaxBatchInputs,
        int MaxConcurrentRequests);
}
