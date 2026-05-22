// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Integrity;

public static class DatabaseIntegrityColumns
{
    public const string VersionProperty = "IntegrityVersion";
    public const string MacProperty = "IntegrityMac";

    public const string VersionColumn = "integrity_version";
    public const string MacColumn = "integrity_mac";
}
