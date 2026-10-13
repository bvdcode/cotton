// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class TruncatedImageMetadataException(Exception innerException)
        : Exception("Image metadata is truncated.", innerException)
    {
    }
}
