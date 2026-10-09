// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Files
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BatchItemKind
    {
        Folder,
        File,
    }
}
