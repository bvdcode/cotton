// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json;

namespace Cotton.Server.Models.Dto
{
    public class PasskeyAssertionOptionsResponseDto
    {
        public string RequestId { get; set; } = null!;

        public JsonElement Options { get; set; }
    }
}
