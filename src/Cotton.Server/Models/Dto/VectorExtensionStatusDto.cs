// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class VectorExtensionStatusDto
    {
        public bool ExtensionEnabled { get; init; }

        public bool ExtensionAvailable { get; init; }

        public int PostgresMajorVersion { get; init; }

        public string DatabaseName { get; init; } = string.Empty;

        public long VectorCount { get; init; }

        public bool IndexReady { get; init; }

        public bool IndexBuilding { get; init; }

        public long IndexSizeBytes { get; init; }

        public string? IndexErrorCode { get; init; }

        public string IndexCreateSql { get; init; } = string.Empty;
    }
}
