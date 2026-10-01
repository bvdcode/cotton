// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Enums
{
    public enum DirectoryFilterOperator
    {
        None,
        Contains,
        DoesNotContain,
        Equals,
        DoesNotEqual,
        StartsWith,
        EndsWith,
        IsEmpty,
        IsNotEmpty,
        IsAnyOf,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }
}
