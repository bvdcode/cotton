// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Computation
{
    public record ComputationStatus(ComputationServiceInfo? Info, int? Dimensions, ComputationError? Error)
    {
        public bool IsReady => Error is null && Info is not null && Dimensions is not null;
    }
}
