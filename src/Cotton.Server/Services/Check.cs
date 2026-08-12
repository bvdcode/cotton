// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    public class Check
    {
        public string Name { get; set; } = null!;

        public string Status { get; set; } = null!;

        public string Description { get; set; } = null!;
    }
}
