// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    public class HealthResponse
    {
        public string Status { get; set; } = null!;

        public Check[] Checks { get; set; } = [];
    }
}
