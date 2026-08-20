// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Http
{
    internal readonly record struct HttpByteRange(long Start, long EndInclusive)
    {
        public long ContentLength => (EndInclusive - Start) + 1;
    }
}
