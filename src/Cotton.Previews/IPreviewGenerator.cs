// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public interface IPreviewGenerator
    {
        string Id { get; }

        int Version { get; }

        int Priority { get; }

        IEnumerable<string> SupportedContentTypes { get; }

        Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size);
    }
}
