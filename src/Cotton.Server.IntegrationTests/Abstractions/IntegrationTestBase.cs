// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class IntegrationTestBase : IDisposable
    {
        public const string DatabaseName = "cotton_dev_tests";
        protected string CurrentDatabaseName { get; }
        protected CottonDbContext DbContext { get; private set; }

        protected IntegrationTestBase()
            : this(GetTestSetting("COTTON_TEST_PG_DATABASE", DatabaseName))
        {
        }

        protected IntegrationTestBase(string databaseName)
        {
            CurrentDatabaseName = databaseName;
            DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new();
            var userBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = TestPostgresHost,
                Port = TestPostgresPort,
                Username = TestPostgresUsername,
                Password = TestPostgresPassword,
                Database = databaseName,
            };
            optionsBuilder.UseNpgsql(userBuilder.ConnectionString, x =>
            {
                x.UseAdminDatabase("postgres");
            });
            DbContext = new(optionsBuilder.Options);
        }

        protected static string TestPostgresHost => GetTestSetting("COTTON_TEST_PG_HOST", "localhost");

        protected static int TestPostgresPort
            => int.Parse(GetTestSetting("COTTON_TEST_PG_PORT", "5432"));

        protected static string TestPostgresUsername
            => GetTestSetting("COTTON_TEST_PG_USERNAME", "postgres");

        protected static string TestPostgresPassword
            => GetTestSetting("COTTON_TEST_PG_PASSWORD", "postgres");

        private static string GetTestSetting(string key, string fallback)
        {
            string? value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public void Dispose()
        {
            DbContext?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
