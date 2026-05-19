// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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
            : this(DatabaseName)
        {
        }

        protected IntegrationTestBase(string databaseName)
        {
            CurrentDatabaseName = databaseName;
            const ushort port = 5432;
            const string host = "localhost";
            const string username = "postgres";
            const string password = "postgres";

            DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new();
            var userBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                Database = databaseName,
            };
            optionsBuilder.UseNpgsql(userBuilder.ConnectionString, x =>
            {
                x.UseAdminDatabase("postgres");
            });
            DbContext = new(optionsBuilder.Options);
        }

        public void Dispose()
        {
            DbContext?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
