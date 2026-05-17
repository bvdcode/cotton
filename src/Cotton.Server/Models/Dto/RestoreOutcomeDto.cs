// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Dto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreStatus
    {
        Restored = 0,
        ParentMissing = 1,
        Conflict = 2,
        NotRestorable = 3,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreConflictKind
    {
        Folder = 0,
        File = 1,
    }

    public class RestoreOutcomeDto
    {
        public RestoreStatus Status { get; set; }
        public string? OriginalParentPath { get; set; }
        public string? MissingPath { get; set; }
        public RestoreConflictKind? ConflictKind { get; set; }
        public string? ConflictName { get; set; }
        public NodeDto? RestoredNode { get; set; }
        public NodeFileManifestDto? RestoredFile { get; set; }
        public string? Reason { get; set; }
    }
}
