// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Npgsql.Models;

namespace Cotton.Server.Services.Search
{
    public static class VectorIndexDefinition
    {
        public const string Name = "ix_file_embeddings_bge_m3_v1";
        public const string Schema = "public";
        public const string Table = "file_embeddings";
        public const string VectorColumn = "embedding";
        public const string VersionColumn = "index_version";
        public const int Version = 1;
        public const int Dimensions = 1024;

        public static string ManualCreateSql => FormattableString.Invariant($"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS {Name}
            ON {Schema}.{Table}
            USING hnsw (({VectorColumn}::vector({Dimensions})) vector_cosine_ops)
            WHERE {VersionColumn} = {Version}
            """);

        public const string ExpectedDefinition =
            "CREATE INDEX ix_file_embeddings_bge_m3_v1 ON public.file_embeddings USING hnsw " +
            "(((embedding)::vector(1024)) vector_cosine_ops) WHERE (index_version = 1)";

        public static bool IsCompatible(PostgresIndexStatus status) => status.Definition == ExpectedDefinition;

        public static bool IsReady(PostgresIndexStatus status) => status.IsValid && IsCompatible(status);
    }
}
