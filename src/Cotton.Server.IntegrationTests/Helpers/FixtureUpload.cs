// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    public record FixtureUpload(
        Guid NodeFileId,
        string FileName,
        string ContentType,
        int SourceLength,
        bool ExpectLargePreview);
}
