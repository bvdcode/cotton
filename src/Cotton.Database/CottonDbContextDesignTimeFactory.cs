// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Cotton.Database;

/// <summary>
/// Creates <see cref="CottonDbContext"/> for EF tooling without booting the server host.
/// </summary>
public class CottonDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CottonDbContext>
{
    /// <inheritdoc />
    public CottonDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CottonDbContext>();
        optionsBuilder.UseNpgsql(BuildConnectionString(), x => x.UseAdminDatabase("postgres"));
        return new CottonDbContext(optionsBuilder.Options);
    }

    private static string BuildConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = GetEnvironment("COTTON_PG_HOST", "localhost"),
            Port = int.Parse(GetEnvironment("COTTON_PG_PORT", "5432")),
            Database = GetEnvironment("COTTON_PG_DATABASE", "cotton_dev"),
            Username = GetEnvironment("COTTON_PG_USERNAME", "postgres"),
            Password = GetEnvironment("COTTON_PG_PASSWORD", "postgres")
        };

        return builder.ConnectionString;
    }

    private static string GetEnvironment(string name, string defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
