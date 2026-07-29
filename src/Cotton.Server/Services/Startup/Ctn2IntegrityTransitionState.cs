// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;
using Cotton.Server.Services;

namespace Cotton.Server.Services.Startup
{
    [Obsolete("OBSOLETE TRANSITION: this marker contract exists only for the 0.5 CTN2/database-integrity cutover. Remove it with the transition startup check.")]
    internal static class Ctn2IntegrityTransitionState
    {
        private const string CompletionStorageMarkerLogicalKey =
            "cotton.ctn2-integrity-rewrite.completed.v1";

        public static string CompletionStorageMarkerKey { get; } = Hasher.ToHexStringHash(
            Hasher.HashData(Encoding.UTF8.GetBytes(CompletionStorageMarkerLogicalKey)));
    }
}
