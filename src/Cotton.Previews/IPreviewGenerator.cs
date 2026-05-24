// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Generates WebP preview images for one or more MIME types.
    /// </summary>
    public interface IPreviewGenerator
    {
        /// <summary>Gets the generator version used to invalidate stale previews.</summary>
        int Version { get; }
        /// <summary>Gets MIME types supported by this generator.</summary>
        IEnumerable<string> SupportedContentTypes { get; }
        /// <summary>Generates a square WebP preview from the supplied source stream.</summary>
        Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size);
    }
}
