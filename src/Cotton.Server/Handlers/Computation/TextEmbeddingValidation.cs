// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Computation;
using Cotton.Server.Services.Search;

namespace Cotton.Server.Handlers.Computation
{
    public static class TextEmbeddingValidation
    {
        public static Uri NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url.Trim().TrimEnd('/') + "/", UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new ComputationException(ComputationError.InvalidUrl);
            }
            return uri;
        }

        public static void ValidateInfo(ComputationServiceInfo info)
        {
            if (!info.IsEmbeddingModel)
            {
                throw new ComputationException(ComputationError.NotEmbeddingModel);
            }
            if (info.ModelId != VectorIndexDefinition.ModelId || info.Pooling != VectorIndexDefinition.Pooling)
            {
                throw new ComputationException(ComputationError.IncompatibleModel);
            }
            if (info.MaxInputTokens <= 0 || info.MaxBatchTokens <= 0
                || info.MaxBatchInputs <= 0 || info.MaxConcurrentRequests <= 0)
            {
                throw new ComputationException(ComputationError.InvalidResponse);
            }
        }

        public static void ValidateVectors(float[][] vectors, int expectedCount)
        {
            if (vectors.Length != expectedCount)
            {
                throw new ComputationException(ComputationError.InvalidVectors);
            }
            foreach (float[] vector in vectors)
            {
                if (vector is null || vector.Length != VectorIndexDefinition.Dimensions)
                {
                    throw new ComputationException(ComputationError.InvalidDimensions);
                }
                if (vector.Any(value => !float.IsFinite(value)) || !vector.Any(value => value != 0))
                {
                    throw new ComputationException(ComputationError.InvalidVectors);
                }
            }
        }
    }
}
