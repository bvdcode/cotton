// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.App.State;

internal sealed class SqliteSyncAppDbContextFactory
{
    private readonly string _databasePath;

    public SqliteSyncAppDbContextFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public SyncAppDbContext Create()
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = _databasePath,
            ["Pooling"] = false,
        }.ToString();
        DbContextOptions<SyncAppDbContext> options = new DbContextOptionsBuilder<SyncAppDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new SyncAppDbContext(options);
    }

    public void EnsureDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
