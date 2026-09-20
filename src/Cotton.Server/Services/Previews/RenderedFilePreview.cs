// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Previews
{
    public record RenderedFilePreview(byte[] Small, byte[]? Large, string GeneratorId, int GeneratorVersion);
}
