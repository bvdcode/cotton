// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Cotton.Database
{
    public partial class CottonDbContext
    {
        [DbFunction("array_to_vector", IsBuiltIn = true)]
        public static Vector ToVector(float[] values, int dimensions, bool explicitCast)
        {
            throw new NotSupportedException();
        }

        [DbFunction("fetchval", IsBuiltIn = true)]
        public static string? GetHstoreValue(Dictionary<string, string>? hstore, string key)
        {
            throw new NotSupportedException();
        }
    }
}
