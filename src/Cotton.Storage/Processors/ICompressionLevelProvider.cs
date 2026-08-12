// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Processors
{
    public interface ICompressionLevelProvider
    {
        int Level { get; }
    }
}
