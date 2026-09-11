// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Cotton.Database
{
    public class CottonDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CottonDbContext>
    {
        public CottonDbContext CreateDbContext(string[] args)
        {
            DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new DbContextOptionsBuilder<CottonDbContext>();
            optionsBuilder.UseNpgsql(BuildConnectionString(), x => x.UseAdminDatabase("postgres"));
            return new CottonDbContext(optionsBuilder.Options);
        }

        private static string BuildConnectionString()
        {
            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
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
}
