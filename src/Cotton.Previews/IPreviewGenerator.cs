// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public interface IPreviewGenerator
    {
        int Version { get; }
        IEnumerable<string> SupportedContentTypes { get; }
        Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size);
    }
}
