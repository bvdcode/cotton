// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    public record FileManifestPreviewState(
        Guid Id,
        byte[]? SmallFilePreviewHash,
        byte[]? SmallFilePreviewHashEncrypted,
        byte[]? LargeFilePreviewHash,
        string? PreviewGenerationError);
}
