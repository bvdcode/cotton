// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class CustomGeoLookupTestResultDto
    {
        public string InputLabel { get; init; } = string.Empty;

        public string InputValue { get; init; } = string.Empty;

        public string? Country { get; init; }

        public string? Region { get; init; }

        public string? City { get; init; }
    }
}
