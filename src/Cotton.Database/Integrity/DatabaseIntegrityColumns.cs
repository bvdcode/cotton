// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Integrity;

/// <summary>Defines EF shadow-property and database column names used for row integrity metadata.</summary>
public static class DatabaseIntegrityColumns
{
    /// <summary>EF shadow property name that stores the integrity schema version.</summary>
    public const string VersionProperty = "IntegrityVersion";
    /// <summary>EF shadow property name that stores the row integrity MAC.</summary>
    public const string MacProperty = "IntegrityMac";

    /// <summary>Database column name that stores the integrity schema version.</summary>
    public const string VersionColumn = "integrity_version";
    /// <summary>Database column name that stores the row integrity MAC.</summary>
    public const string MacColumn = "integrity_mac";
}
