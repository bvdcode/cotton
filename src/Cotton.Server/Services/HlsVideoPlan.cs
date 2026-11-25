// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Core.Timing;

namespace Cotton.Server.Services
{
    internal record HlsVideoPlan(int VideoStreamIndex, int? AudioStreamIndex, IReadOnlyList<MediaTimeRange> Segments);
}
