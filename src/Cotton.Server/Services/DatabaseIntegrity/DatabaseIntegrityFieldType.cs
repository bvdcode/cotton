// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

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
