// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Exceptions;
using System.Net;

namespace Cotton.Server.Models.Computation
{
    public class ComputationException(ComputationError error)
        : WebApiException(
            HttpStatusCode.UnprocessableEntity,
            string.Empty,
            $"Text embedding service validation failed: {error}.",
            new Dictionary<string, object?> { ["code"] = error.ToString() })
    {
        public ComputationError Error { get; } = error;
    }
}
