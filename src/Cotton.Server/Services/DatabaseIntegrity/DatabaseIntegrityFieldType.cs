// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

// Field tags are part of the signed binary format. Append new values only; changing existing numeric values invalidates
// every stored row MAC for the current format version.
internal enum DatabaseIntegrityFieldType : byte
{
    String = 1,
    Guid = 2,
    Bytes = 3,
    Boolean = 4,
    Int32 = 5,
    Int64 = 6,
    DateTime = 7,
    StringArray = 8,
    StringDictionary = 9
}
