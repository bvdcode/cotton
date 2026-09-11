// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Database
{
    public partial class CottonDbContext
    {
        [DbFunction("fetchval", IsBuiltIn = true)]
        public static string? GetHstoreValue(Dictionary<string, string>? hstore, string key)
        {
            throw new NotSupportedException();
        }
    }
}
