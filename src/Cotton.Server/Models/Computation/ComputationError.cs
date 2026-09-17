// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Computation
{
    [JsonConverter(typeof(JsonStringEnumConverter<ComputationError>))]
    public enum ComputationError
    {
        NotConfigured,
        UnsupportedMode,
        InvalidUrl,
        Unreachable,
        Timeout,
        InvalidResponse,
        NotEmbeddingModel,
        IncompatibleModel,
        InvalidDimensions,
        InvalidVectors,
        InvalidInput
    }
}
