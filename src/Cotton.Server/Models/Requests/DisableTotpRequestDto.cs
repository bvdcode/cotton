// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public record DisableTotpRequestDto
    {
        public string Password { get; init; } = null!;
    }
}
